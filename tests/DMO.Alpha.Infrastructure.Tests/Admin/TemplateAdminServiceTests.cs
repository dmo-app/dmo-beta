using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// Prova a leitura de fundação da vista Admin → Templates (Step B): as
/// linhas persistidas projetam a identidade estável (TemplateKey), o
/// rótulo corrente e as capabilities módulo-owned — os factos em que a
/// resolução de acesso efectivo ancora. A forma canónica de conjunto
/// (de-duplicada, ordenada) é a forma persistida; a leitura devolve
/// exatamente o que está persistido, sem inventar estados.
/// </summary>
public sealed class TemplateAdminServiceTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly TemplateAdminService _service;

    public TemplateAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _service = new TemplateAdminService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ListTemplates_EmptyPersistence_ReturnsEmptyList()
    {
        var templates = await _service.ListTemplatesAsync();

        Assert.Empty(templates);
    }

    [Fact]
    public async Task ListTemplates_ProjectsPersistedFactsOrderedByStableKey()
    {
        _db.Templates.AddRange(
            new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-responsavel-operacional",
                Name = "Responsável Operacional",
                Capabilities =
                [
                    DmoCapabilities.JobOnCreate,
                    DmoCapabilities.ControloCreate,
                    DmoCapabilities.ControloApprove
                ]
            },
            new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-operador-beta",
                Name = "Operador Beta",
                Capabilities = [DmoCapabilities.JobOnCreate]
            });
        await _db.SaveChangesAsync();

        var templates = await _service.ListTemplatesAsync();

        Assert.Equal(2, templates.Count);
        // Ordenação determinística pela identidade estável.
        Assert.Equal("tpl-operador-beta", templates[0].TemplateKey);
        Assert.Equal("tpl-responsavel-operacional", templates[1].TemplateKey);

        var responsable = templates.Single(t => t.TemplateKey == "tpl-responsavel-operacional");
        Assert.Equal("Responsável Operacional", responsable.Name);
        Assert.Equal(
            new[] { DmoCapabilities.ControloApprove, DmoCapabilities.ControloCreate, DmoCapabilities.JobOnCreate },
            responsable.Capabilities);

        var operador = templates.Single(t => t.TemplateKey == "tpl-operador-beta");
        Assert.Equal("Operador Beta", operador.Name);
        Assert.Equal([DmoCapabilities.JobOnCreate], operador.Capabilities);
    }

    [Fact]
    public async Task ListTemplates_RenamedLabel_IsProjectedCurrentLabelWithStableKey()
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = "tpl-responsavel-operacional",
            Name = "Responsável Operacional",
            Capabilities = [DmoCapabilities.ControloApprove]
        };
        _db.Templates.Add(template);
        await _db.SaveChangesAsync();

        template.Name = "Etiqueta Nova";
        await _db.SaveChangesAsync();

        var projected = (await _service.ListTemplatesAsync()).Single();

        // O rótulo é o corrente; a identidade estável é a persistida.
        Assert.Equal("tpl-responsavel-operacional", projected.TemplateKey);
        Assert.Equal("Etiqueta Nova", projected.Name);
        Assert.Equal([DmoCapabilities.ControloApprove], projected.Capabilities);
    }

    // ------------------------------------------------------------------
    // CreateTemplate / EditTemplate (D6): criar/editar com Nome + as 3
    // capabilities Beta; TemplateKey MINTADA pelo backend («tpl-» + 8
    // hex); zero capabilities permitido («Sem capabilities»).
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateTemplate_MintsStableKeyAndPersistsCanonicalCapabilities()
    {
        var outcome = await _service.CreateTemplateAsync(
            new CreateTemplate(
                "Responsável Operacional",
                [
                    DmoCapabilities.ControloApprove,
                    DmoCapabilities.JobOnCreate,
                    DmoCapabilities.ControloApprove
                ]));

        // A key é MINTADA pelo backend («tpl-» + 8 hex minúsculos) — nunca
        // transportada pelo comando, nunca derivada do rótulo.
        Assert.Equal(CreateTemplateResult.Success, outcome.Result);
        Assert.NotNull(outcome.Template);
        Assert.Matches(@"^tpl-[0-9a-f]{8}$", outcome.Template!.TemplateKey);
        Assert.Equal("Responsável Operacional", outcome.Template.Name);

        // A forma persistida é o conjunto canónico (de-duplicado e
        // ordenado) — o round-trip pela leitura prova a conversão.
        var persisted = (await _service.ListTemplatesAsync()).Single();
        Assert.Equal(outcome.Template.TemplateKey, persisted.TemplateKey);
        Assert.Equal(
            new[] { DmoCapabilities.ControloApprove, DmoCapabilities.JobOnCreate },
            persisted.Capabilities);
    }

    [Fact]
    public async Task CreateTemplate_ZeroCapabilities_IsAllowed()
    {
        // Zero capabilities é uma template VÁLIDA («Sem capabilities») —
        // o Beta não inventa simetria de permissões.
        var outcome = await _service.CreateTemplateAsync(new CreateTemplate("Consulta", []));

        Assert.Equal(CreateTemplateResult.Success, outcome.Result);
        Assert.Empty((await _service.ListTemplatesAsync()).Single().Capabilities);
    }

    [Theory]
    [InlineData("", CreateTemplateResult.MissingName)]
    [InlineData("   ", CreateTemplateResult.MissingName)]
    [InlineData("Nome", CreateTemplateResult.UnknownCapability)]
    public async Task CreateTemplate_InvariantViolations_AreTypedRefusals(
        string name, CreateTemplateResult expected)
    {
        // O matrix de permissões por módulo do mockup NÃO é vocabulário
        // Beta: jobon.view/controlo.edit/peso.aprovar/admin.gerir são
        // recusados tipadamente (UnknownCapability).
        var outcome = await _service.CreateTemplateAsync(
            new CreateTemplate(name, [DmoCapabilities.JobOnCreate, "jobon.view"]));

        Assert.Equal(expected, outcome.Result);
        Assert.Null(outcome.Template);
        Assert.Empty(_db.Templates.ToList());
    }

    // O mesmo pedido outra vez (deep sweep, 18/10/2026, B-admin-6): a rede
    // caiu a meio de «Criar template», o servidor criou-a e o operador voltou
    // a carregar. O segundo pedido criava outra com o mesmo nome; agora abre
    // a que já foi criada, como a resposta ao primeiro.
    [Fact]
    public async Task CreateTemplate_TheSameRequestAgain_OpensTheTemplateAlreadyCreated()
    {
        var first = await _service.CreateTemplateAsync(
            new CreateTemplate("Offline 1", [DmoCapabilities.ControloCreate, DmoCapabilities.JobOnCreate]));
        // O mesmo pedido: o mesmo nome e o mesmo conjunto (noutra ordem).
        var again = await _service.CreateTemplateAsync(
            new CreateTemplate(" Offline 1 ", [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.JobOnCreate]));

        Assert.Equal(CreateTemplateResult.Success, again.Result);
        Assert.Equal(first.Template!.TemplateKey, again.Template!.TemplateKey);
        Assert.Equal("Offline 1", again.Template.Name);
        Assert.Single(await _db.Templates.AsNoTracking().ToListAsync());

        // Outro pedido cria outra: outras capabilities, ou o mesmo nome criado
        // há uma hora (fora da janela do mesmo pedido).
        var other = await _service.CreateTemplateAsync(new CreateTemplate("Offline 1", [DmoCapabilities.JobOnCreate]));
        Assert.Equal(CreateTemplateResult.Success, other.Result);
        Assert.NotEqual(first.Template.TemplateKey, other.Template!.TemplateKey);
        _db.Templates.Add(new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = "tpl-antiga00",
            Name = "Antiga",
            Capabilities = [],
            CreatedAtUtc = DateTimeOffset.UtcNow - TimeSpan.FromHours(1)
        });
        await _db.SaveChangesAsync();
        var old = await _service.CreateTemplateAsync(new CreateTemplate("Antiga", []));
        Assert.NotEqual("tpl-antiga00", old.Template!.TemplateKey);
        Assert.Equal(4, await _db.Templates.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateTemplate_MintedKeysAreUniqueAndWellFormed()
    {
        // O mint é aleatório criptográfico com re-mint: um lote de keys
        // mintadas é único e bem formado (a unicidade persistida é o índice
        // único; o re-mint mantém a promessa contra o registo real).
        var keys = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            var outcome = await _service.CreateTemplateAsync(
                new CreateTemplate($"Template {i}", [DmoCapabilities.JobOnCreate]));
            Assert.Equal(CreateTemplateResult.Success, outcome.Result);
            keys.Add(outcome.Template!.TemplateKey);
        }

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches(@"^tpl-[0-9a-f]{8}$", key));
        Assert.Equal(25, (await _service.ListTemplatesAsync()).Count);
    }

    [Fact]
    public async Task EditTemplate_RenamesLabelEditsCapabilities_NeverRewritesTheStableKey()
    {
        var created = await _service.CreateTemplateAsync(
            new CreateTemplate("Operador Beta", [DmoCapabilities.JobOnCreate]));

        var outcome = await _service.EditTemplateAsync(
            new EditTemplate(
                created.Template!.TemplateKey,
                "Responsável Operacional (renomeado)",
                [DmoCapabilities.ControloCreate]));

        Assert.Equal(EditTemplateResult.Success, outcome.Result);
        Assert.Equal(created.Template.TemplateKey, outcome.Template!.TemplateKey);

        var persisted = (await _service.ListTemplatesAsync()).Single();
        Assert.Equal(created.Template.TemplateKey, persisted.TemplateKey);
        Assert.Equal("Responsável Operacional (renomeado)", persisted.Name);
        Assert.Equal(
            new[] { DmoCapabilities.ControloCreate },
            persisted.Capabilities);

        // A key estável é o que a atribuição ancora: renomear NÃO muda o
        // acesso de ninguém (provado no AdminAuthorizationServiceTests).
    }

    [Fact]
    public async Task CreateAndEditTemplate_BothControlos_AreSaved()
    {
        // Aviso em vez de bloqueio (o Owner 2026-10-16): o Controlo Criar e
        // o Aprovar são páginas separadas, por isso podem estar juntos.
        var create = await _service.CreateTemplateAsync(
            new CreateTemplate("Ambos", [DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]));
        Assert.Equal(CreateTemplateResult.Success, create.Result);

        var valid = await _service.CreateTemplateAsync(
            new CreateTemplate("Valida", [DmoCapabilities.ControloCreate]));
        var edit = await _service.EditTemplateAsync(
            new EditTemplate(valid.Template!.TemplateKey, "Valida", [
                DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]));
        Assert.Equal(EditTemplateResult.Success, edit.Result);
        var edited = (await _service.ListTemplatesAsync()).Single(t => t.Name == "Valida");
        Assert.Equal([DmoCapabilities.ControloApprove, DmoCapabilities.ControloCreate], edited.Capabilities);
    }

    [Fact]
    public async Task EditTemplate_UnknownStableKey_MissingNameOrNonBetaCapability_TypedRefusals()
    {
        var created = await _service.CreateTemplateAsync(
            new CreateTemplate("Operador Beta", [DmoCapabilities.JobOnCreate]));

        var unknown = await _service.EditTemplateAsync(
            new EditTemplate("tpl-inexistente", "Outro Nome", []));
        Assert.Equal(EditTemplateResult.UnknownTemplate, unknown.Result);

        var missingName = await _service.EditTemplateAsync(
            new EditTemplate(created.Template!.TemplateKey, "   ", []));
        Assert.Equal(EditTemplateResult.MissingName, missingName.Result);

        var nonBeta = await _service.EditTemplateAsync(
            new EditTemplate(created.Template.TemplateKey, "Nome", ["controlo.edit"]));
        Assert.Equal(EditTemplateResult.UnknownCapability, nonBeta.Result);

        // Nenhuma recusa tocou a linha persistida.
        var untouched = (await _service.ListTemplatesAsync()).Single();
        Assert.Equal("Operador Beta", untouched.Name);
        Assert.Equal([DmoCapabilities.JobOnCreate], untouched.Capabilities);
    }

    [Fact]
    public async Task DeleteTemplate_Unassigned_RemovesTheRow()
    {
        var created = await _service.CreateTemplateAsync(
            new CreateTemplate("Temporária", [DmoCapabilities.JobOnCreate]));

        var outcome = await _service.DeleteTemplateAsync(created.Template!.TemplateKey);

        Assert.Equal(DeleteTemplateResult.Success, outcome.Result);
        Assert.Empty(await _service.ListTemplatesAsync());
    }

    [Fact]
    public async Task DeleteTemplate_StillAssigned_RefusesWithTheUserCount()
    {
        var created = await _service.CreateTemplateAsync(
            new CreateTemplate("Operador", [DmoCapabilities.JobOnCreate]));
        var templateId = created.Template!.Id;
        foreach (var n in new[] { "1001", "1002" })
        {
            var user = new DMO.Alpha.Core.Identity.User
            {
                Id = Guid.NewGuid(),
                Name = $"Operador {n}",
                OperatorId = n,
                ProviderUserId = $"p-{n}"
            };
            _db.Users.Add(user);
            _db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = templateId });
        }
        await _db.SaveChangesAsync();

        var outcome = await _service.DeleteTemplateAsync(created.Template.TemplateKey);

        Assert.Equal(DeleteTemplateResult.InUse, outcome.Result);
        Assert.Equal(2, outcome.AssignedUsers);
        Assert.Single(await _service.ListTemplatesAsync());
        Assert.Equal(2, await _db.TemplateAssignments.CountAsync());
    }

    [Fact]
    public async Task DeleteTemplate_UnknownKey_TypedRefusal()
    {
        var outcome = await _service.DeleteTemplateAsync("tpl-inexistente");

        Assert.Equal(DeleteTemplateResult.UnknownTemplate, outcome.Result);
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// Prova as semânticas de autorização da fundação Admin (Step B) sobre a
/// relação persistida User↔Template (TEMPLATES.md):
///
/// - SEM Template corrente → sem capabilities derivadas de Template
///   ("no-template access");
/// - stand-by → acesso operacional corrente DESLIGADO, com o User, a
///   atribuição e a identidade estável preservados (USERS.md §1);
/// - RENOMEAR um Template (rótulo) nunca muda o acesso efectivo — a
///   resolução ancora na identidade estável (TemplateKey), nunca no
///   rótulo (TEMPLATES.md §"Template meaning");
/// - boundary ADMIN canónico: a atribuição de Template RECUSA uma
///   identidade associada ao ADMIN corrente, e a resolução trata sempre o
///   ADMIN como SEM Template — mesmo perante uma linha de atribuição
///   persistida (decision review 20261010: D1);
/// - user inexistente → resolução null (nada é inventado).
/// </summary>
public sealed class AdminAuthorizationServiceTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly AdminAuthorizationService _service;

    public AdminAuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _service = new AdminAuthorizationService(_db, new AdminEmailSettings(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ------------------------------------------------------------------
    // Seeding helpers
    // ------------------------------------------------------------------

    private async Task<User> SeedUserAsync(
        string operatorId,
        string providerUserId,
        bool isStandBy = false,
        string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = $"Operador {operatorId}",
            ProviderUserId = providerUserId,
            IsStandBy = isStandBy,
            Email = email
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Template> SeedTemplateAsync(
        string templateKey,
        string name,
        params string[] capabilities)
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateKey,
            Name = name,
            Capabilities = capabilities
        };

        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    private async Task SeedAssignmentAsync(User user, Template template)
    {
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Semeia o email do ADMIN nas Definições (o ADMIN é o email — o Owner
    /// 2026-10-16) — o valor da comparação do boundary.
    /// </summary>
    private async Task SeedAdminEmailAsync(string adminEmail)
    {
        _db.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = adminEmail
        });
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------
    // Resolution: no current Template → no Template-derived capabilities
    // ------------------------------------------------------------------

    [Fact]
    public async Task Resolve_NoCurrentTemplate_HasNoTemplateDerivedCapabilities()
    {
        var user = await SeedUserAsync("1234", "provider-user-1234");

        var access = await _service.ResolveEffectiveAccessAsync(user.Id);

        Assert.NotNull(access);
        Assert.Empty(access!.Capabilities);
        Assert.False(access.HasCapability(DmoCapabilities.JobOnCreate));
        Assert.Null(access.TemplateId);
        Assert.Null(access.TemplateKey);
        Assert.Null(access.TemplateName);
        Assert.False(access.IsStandBy);
        Assert.False(access.IsAdmin);
    }

    [Fact]
    public async Task Resolve_WithCurrentTemplate_ResolvesTheTemplateCapabilities()
    {
        var user = await SeedUserAsync("2345", "provider-user-2345");
        var template = await SeedTemplateAsync(
            "tpl-responsavel-operacional",
            "Responsável Operacional",
            DmoCapabilities.JobOnCreate,
            DmoCapabilities.ControloApprove);
        await SeedAssignmentAsync(user, template);

        var access = await _service.ResolveEffectiveAccessAsync(user.Id);

        Assert.NotNull(access);
        Assert.Equal(template.Id, access!.TemplateId);
        Assert.Equal("tpl-responsavel-operacional", access.TemplateKey);
        Assert.Equal("Responsável Operacional", access.TemplateName);
        Assert.True(access.HasCapability(DmoCapabilities.JobOnCreate));
        Assert.True(access.HasCapability(DmoCapabilities.ControloApprove));
        Assert.False(access.HasCapability(DmoCapabilities.ControloCreate));
        Assert.Equal(2, access.Capabilities.Count);
    }

    [Fact]
    public async Task Resolve_TemplateWithBothControlos_GrantsBoth()
    {
        var user = await SeedUserAsync("0033", "provider-conflicting");
        var template = await SeedTemplateAsync("tpl-legacy", "Qualquer nome",
            DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove);
        await SeedAssignmentAsync(user, template);
        var access = await _service.ResolveEffectiveAccessAsync(user.Id);
        Assert.NotNull(access);
        Assert.True(access!.HasCapability(DmoCapabilities.JobOnCreate));
        Assert.True(access.HasCapability(DmoCapabilities.ControloCreate));
        Assert.True(access.HasCapability(DmoCapabilities.ControloApprove));
    }

    // ------------------------------------------------------------------
    // Stand-by: access disabled, configuration preserved
    // ------------------------------------------------------------------

    [Fact]
    public async Task Resolve_StandBy_DisablesCurrentAccessButPreservesAssignmentFacts()
    {
        var user = await SeedUserAsync("3456", "provider-user-3456", isStandBy: true);
        var template = await SeedTemplateAsync(
            "tpl-operador-controlo",
            "Operador de Controlo",
            DmoCapabilities.JobOnCreate,
            DmoCapabilities.ControloCreate);
        await SeedAssignmentAsync(user, template);

        var access = await _service.ResolveEffectiveAccessAsync(user.Id);

        Assert.NotNull(access);
        Assert.True(access!.IsStandBy);
        // Stand-by desliga o acesso operacional corrente — o User e a
        // atribuição ficam preservados (a resolução continua a reportar os
        // factos do Template; as capabilities é que ficam vazias).
        Assert.Empty(access.Capabilities);
        Assert.False(access.HasCapability(DmoCapabilities.JobOnCreate));
        Assert.False(access.HasCapability(DmoCapabilities.ControloCreate));
        Assert.Equal(template.Id, access.TemplateId);
        Assert.Equal("tpl-operador-controlo", access.TemplateKey);
    }

    // ------------------------------------------------------------------
    // Renamed templates: the label never owns access
    // ------------------------------------------------------------------

    [Fact]
    public async Task Resolve_RenamedTemplate_EffectiveAccessIsUnchanged()
    {
        var user = await SeedUserAsync("4567", "provider-user-4567");
        var template = await SeedTemplateAsync(
            "tpl-responsavel-operacional",
            "Responsável Operacional",
            DmoCapabilities.JobOnCreate,
            DmoCapabilities.ControloApprove);
        await SeedAssignmentAsync(user, template);

        var before = await _service.ResolveEffectiveAccessAsync(user.Id);
        Assert.NotNull(before);
        Assert.True(before!.HasCapability(DmoCapabilities.ControloApprove));
        Assert.Equal("tpl-responsavel-operacional", before.TemplateKey);

        // Renomear: o rótulo muda, a identidade estável NÃO.
        template.Name = "Responsável Operacional (renomeado)";
        template.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var after = await _service.ResolveEffectiveAccessAsync(user.Id);

        // O acesso efectivo é idêntico — a resolução ancora na identidade
        // estável, nunca no rótulo (o rótulo renomeado é apenas display).
        Assert.NotNull(after);
        Assert.Equal(before.TemplateKey, after!.TemplateKey);
        Assert.Equal(before.TemplateId, after.TemplateId);
        Assert.Equal(before.Capabilities, after.Capabilities);
        Assert.True(after.HasCapability(DmoCapabilities.JobOnCreate));
        Assert.True(after.HasCapability(DmoCapabilities.ControloApprove));
        // O facto de display reflecte o rótulo corrente (não autoriza nada).
        Assert.Equal("Responsável Operacional (renomeado)", after.TemplateName);
    }

    // ------------------------------------------------------------------
    // ADMIN boundary (canonical): ADMIN never has a Template
    // ------------------------------------------------------------------

    [Fact]
    public async Task Resolve_AdminEmailUser_IsAlwaysResolvedWithoutTemplate()
    {
        await SeedAdminEmailAsync("admin-1@example.test");
        var user = await SeedUserAsync("9000", "provider-admin-1", email: "admin-1@example.test");
        var template = await SeedTemplateAsync(
            "tpl-responsavel-operacional",
            "Responsável Operacional",
            DmoCapabilities.JobOnCreate);

        // Uma linha de atribuição persistida contra o boundary (dados
        // inconsistentes não devem nunca transformar-se em acesso):
        // a resolução canónica trata ADMIN como SEM Template.
        await SeedAssignmentAsync(user, template);

        var access = await _service.ResolveEffectiveAccessAsync(user.Id);

        Assert.NotNull(access);
        Assert.True(access!.IsAdmin);
        Assert.Null(access.TemplateId);
        Assert.Null(access.TemplateKey);
        Assert.Null(access.TemplateName);
        Assert.Empty(access.Capabilities);
        Assert.False(access.HasCapability(DmoCapabilities.JobOnCreate));
    }

    [Fact]
    public async Task Resolve_AdminEmailUserWithoutAssignmentRow_AlsoHasNoCapabilities()
    {
        await SeedAdminEmailAsync("admin-2@example.test");
        var user = await SeedUserAsync("9001", "provider-admin-2", email: "admin-2@example.test");

        var access = await _service.ResolveEffectiveAccessAsync(user.Id);

        Assert.NotNull(access);
        Assert.True(access!.IsAdmin);
        Assert.Empty(access.Capabilities);
    }

    [Fact]
    public async Task Resolve_UnknownUser_ReturnsNull()
    {
        Assert.Null(await _service.ResolveEffectiveAccessAsync(Guid.NewGuid()));
    }

    // ------------------------------------------------------------------
    // Assignment authorization: the ADMIN-boundary refusal
    // ------------------------------------------------------------------

    [Fact]
    public async Task AuthorizeTemplateAssignment_AdminEmailUser_IsRefused()
    {
        await SeedAdminEmailAsync("admin-3@example.test");
        var user = await SeedUserAsync("9002", "provider-admin-3", email: "admin-3@example.test");

        var decision = await _service.AuthorizeTemplateAssignmentAsync(user.Id);

        Assert.Equal(TemplateAssignmentAuthorization.RefusedAdminBoundary, decision);
    }

    [Fact]
    public async Task AuthorizeTemplateAssignment_NormalUser_IsAllowed()
    {
        await SeedAdminEmailAsync("admin-4@example.test");
        var user = await SeedUserAsync("5678", "provider-user-5678");

        var decision = await _service.AuthorizeTemplateAssignmentAsync(user.Id);

        Assert.Equal(TemplateAssignmentAuthorization.Allowed, decision);
    }

    [Fact]
    public async Task AuthorizeTemplateAssignment_UnknownUser_IsUserNotFound()
    {
        var decision = await _service.AuthorizeTemplateAssignmentAsync(Guid.NewGuid());

        Assert.Equal(TemplateAssignmentAuthorization.UserNotFound, decision);
    }
}

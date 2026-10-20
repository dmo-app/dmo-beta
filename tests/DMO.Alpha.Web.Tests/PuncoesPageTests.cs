using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A folha PU NNPB (o Owner 17/10/2026): numa produção cuja CM é NNPB, o
/// operador regista as punções pela página, a lista mostra «Alteração»
/// num número repetido e os totais, o Resumo ganha o cartão PU NNPB e o
/// PDF da folha descarrega em memória; numa produção PS nada é registado.
/// </summary>
public partial class PuncoesPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Puncoes";

    private readonly CustomWebApplicationFactory _factory;

    public PuncoesPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PuncaoInspecoes.RemoveRange(db.PuncaoInspecoes);
        db.Puncoes.RemoveRange(db.Puncoes);
        db.ControloViewings.RemoveRange(db.ControloViewings);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Machines.RemoveRange(db.Machines);
        db.Tools.RemoveRange(db.Tools);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        // O user operacional com O SEU TEMPLATE (a regra do corte I: «User
        // sem Template = sem operacional»): os POSTs do Controlo exigem a
        // capability (o modo chefe: o approve não cria).
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = new[]
                {
                    DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate,
                    DMO.Alpha.Core.Admin.DmoCapabilities.ControloCreate
                }
            };
            db.Templates.Add(template);
            db.SaveChanges();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment
        {
            UserId = user.Id,
            TemplateId = template.Id
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password)
    {
        var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<(int JobOnId, int ControloId)> SeedProductionAsync(CmProcess process)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "C2" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "CM-PU", Type = ToolType.Cm, Reference = "1075C142", Lot = "66" });
        db.Tools.Add(new Tool { ToolId = "MF-PU", Type = ToolType.Mf, Reference = "1075", Lot = "83" });
        db.Tools.Add(new Tool { ToolId = "BQ-PU", Type = ToolType.Bq, Reference = "C142", Lot = "165" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "CM-PU", Process = process });
        await db.SaveChangesAsync();

        var created = await scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>().HandleAsync(
            new CreateJobOn("202604", "1075C142", machine.Id, "CM-PU", "MF-PU", "BQ-PU"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        var jobOnId = created.JobOn!.JobOnId;
        var controloId = await db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        return (jobOnId, controloId);
    }

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, int controloId, string handler, params (string, string)[] fields)
    {
        // A folha de uma produção PS não tem formulários: o token vem do Resumo.
        var page = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)
            ?? await GetVerificationTokenAsync(client, "/Controlo/Resumo?jobon_id=" + await JobOnIdAsync(controloId));
        var form = new List<(string, string)> { ("controlo_id", controloId.ToString()), ("__RequestVerificationToken", token) };
        form.AddRange(fields);
        return await client.PostAsync($"{PagePath}?handler={handler}", HtmlFormHelpers.BuildFormPost(form.ToArray()));
    }

    private async Task<int> JobOnIdAsync(int controloId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DmoDbContext>().Controlos
            .Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
    }

    [Fact]
    public async Task NnpbProduction_RegistersPunches_ShowsAlteracao_TotalsResumoAndPdf()
    {
        var (jobOnId, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4311", "Operador Punções", "provider-puncoes-1", "PuncoesPass1!");
        using var client = await LoginClientAsync("4311", "PuncoesPass1!");

        var empty = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("Identificação da produção", empty);
        Assert.Contains("value=\"1075\"", empty);
        Assert.Contains("Sem punções registadas", empty);

        foreach (var (number, lot) in new[] { ("41", "1"), ("42", "1"), ("41", "2") })
        {
            var added = await PostAsync(client, controloId, "Add", ("numero", number), ("referencia", "1075"), ("lote", lot));
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
            Assert.DoesNotContain("recusa", added.Headers.Location!.ToString());
        }

        int firstId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var rows = await db.PuncaoInspecoes.AsNoTracking().OrderBy(i => i.Position).ToListAsync();
            Assert.Equal(new[] { "41", "42", "41" }, rows.Select(r => r.Number));
            Assert.All(rows, r => Assert.Equal("4311", r.RegisteredBy));
            firstId = rows[0].Id;
        }

        var unseen = await PostAsync(client, controloId, "Seen", ("inspecao_id", firstId.ToString()), ("campo", "arrefecedor"), ("visto", "false"));
        Assert.Equal(HttpStatusCode.Redirect, unseen.StatusCode);

        var sheet = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sheet, "class=\"pu-alteracao\""));
        Assert.Contains("<span>Total de punções</span><strong>3</strong>", sheet);
        Assert.Contains("<span>Punções vistas</span><strong>3</strong>", sheet);
        Assert.Contains("<span>Arrefecedores vistos</span><strong>2</strong>", sheet);
        Assert.Contains("value=\"2\"", sheet);
        // Gravar sem recarregar: a folha (identificação, totais, lista e PDF)
        // é a zona que troca; os cartões que gravam estão em data-dmo-parcial.
        Assert.Contains("class=\"pu-page dmo-workspace\" aria-labelledby=\"pu-title\" data-dmo-troca", sheet);
        Assert.Contains("<div id=\"puFolha\" class=\"dmo-zona\" data-dmo-regiao>", sheet);
        Assert.Contains("id=\"puncoes-identificacao\" aria-labelledby=\"pu-id-title\" data-dmo-parcial", sheet);
        Assert.Contains("id=\"puncoes-lista\" aria-labelledby=\"pu-list-title\" data-dmo-parcial", sheet);
        Assert.Contains("document.addEventListener(\"input\"", sheet);

        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains("resumo-linked-pu", resumo);
        Assert.Contains("Existem punções por verificar", resumo);
        Assert.Contains($"href=\"/Controlo/Puncoes?controlo_id={controloId}\"", resumo);
        Assert.Contains(">PDF das PU NNPB</button>", resumo);

        var pdf = await PostAsync(client, controloId, "GeneratePdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("PU-NNPB-1075C142-202604.pdf", pdf.Content.Headers.ContentDisposition?.FileNameStar ?? pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var remove = await PostAsync(client, controloId, "Remove", ("inspecao_id", firstId.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        var afterRemove = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("<span>Total de punções</span><strong>2</strong>", afterRemove);
        Assert.DoesNotContain("class=\"pu-alteracao\"", afterRemove);
    }

    [Fact]
    public async Task NumberWithQuotes_IsWrittenAsText_NeverInsideAnOnclick()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4319", "Operador Aspas", "provider-puncoes-9", "PuncoesPass9!");
        using var client = await LoginClientAsync("4319", "PuncoesPass9!");

        const string hostil = "');alert(1);('";
        var added = await PostAsync(client, controloId, "Add", ("numero", hostil), ("referencia", "1075"), ("lote", "1"));
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);

        var raw = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        // Nada da folha corre como JavaScript a partir de um atributo: remover
        // abre a janela da casa, com o número escrito como texto.
        Assert.DoesNotContain("onclick=", raw);
        Assert.Contains("data-dmo-dialog=\"pu-remover-", raw);
        Assert.Contains("class=\"pu-remove\"", raw);
        Assert.Contains("Remover a punção " + hostil + " · lote 1 deste registo?", WebUtility.HtmlDecode(raw));
    }

    [Fact]
    public async Task RefusedLot_KeepsTheTypedValues_AndWritesNothing()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4312", "Operador Punções", "provider-puncoes-2", "PuncoesPass2!");
        using var client = await LoginClientAsync("4312", "PuncoesPass2!");

        var refused = await PostAsync(client, controloId, "Add", ("numero", "41"), ("referencia", "1075"), ("lote", "A"));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, refused.Headers.Location!.ToString()));
        Assert.Contains("Indique o lote com um número inteiro", html);
        Assert.Contains("id=\"pu-numero\" name=\"numero\" maxlength=\"40\" required value=\"41\"", html);

        using var scope = _factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<DmoDbContext>().Puncoes);
    }

    [Fact]
    public async Task CmWithoutProcess_TheNoticeLinksToTheCmSheet_OnlyWithFerramentas()
    {
        // O Diogo (19/10/2026): os avisos levam à página onde se resolvem. O
        // processo da CM preenche-se na ficha dela (Ferramentas); sem esse
        // módulo, o aviso fica sem link.
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.Single(t => t.ToolId == "CM-PU").Process = null;
            await db.SaveChangesAsync();
        }

        SeedUser("4316", "Operador Punções", "provider-puncoes-6", "PuncoesPass6!");
        using (var semFerramentas = await LoginClientAsync("4316", "PuncoesPass6!"))
        {
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(semFerramentas, $"{PagePath}?controlo_id={controloId}"));
            Assert.Contains("A CM desta produção não tem o processo preenchido.", html);
            Assert.DoesNotContain("dmo-aviso-link", html);
        }

        string[] antes;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = db.Templates.Single(t => t.TemplateKey == "tpl-teste-operacional");
            antes = template.Capabilities.ToArray();
            template.Capabilities = antes.Append(DMO.Alpha.Core.Admin.DmoCapabilities.Ferramentas).ToArray();
            await db.SaveChangesAsync();
        }

        try
        {
            using var comFerramentas = await LoginClientAsync("4316", "PuncoesPass6!");
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(comFerramentas, $"{PagePath}?controlo_id={controloId}"));
            Assert.Contains($"<a class=\"dmo-aviso-link\" href=\"/Ferramentas/Ficha?tool_id=CM-PU&tecnica=1&continuar=%2FControlo%2FPuncoes%3Fcontrolo_id%3D{controloId}\">Preencher o processo na ficha da CM</a>", html);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Templates.Single(t => t.TemplateKey == "tpl-teste-operacional").Capabilities = antes;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PsProduction_HasNoRegisto_NoCardAndNoWrite()
    {
        var (jobOnId, controloId) = await SeedProductionAsync(CmProcess.Ps);
        SeedUser("4313", "Operador Punções", "provider-puncoes-3", "PuncoesPass3!");
        using var client = await LoginClientAsync("4313", "PuncoesPass3!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("Esta produção é PS", html);
        Assert.DoesNotContain("id=\"pu-numero\"", html);

        var refused = await PostAsync(client, controloId, "Add", ("numero", "41"), ("referencia", "1075"), ("lote", "1"));
        Assert.Contains("recusa=NotNnpb", refused.Headers.Location!.ToString());

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.DoesNotContain("resumo-linked-pu", resumo);
        Assert.DoesNotContain("PDF das PU NNPB", resumo);

        using var scope = _factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<DmoDbContext>().Puncoes);
    }

    private async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string tokenPage, string handler, params (string, string)[] fields)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client, tokenPage))!;
        var form = new List<(string, string)> { ("__RequestVerificationToken", token) };
        form.AddRange(fields);
        return await client.PostAsync($"{PagePath}?handler={handler}", HtmlFormHelpers.BuildFormPost(form.ToArray()));
    }

    [Fact]
    public async Task WithoutJobOn_RegisterStandsAlone_AndIsAssociatedByHand()
    {
        // O Owner 17/10/2026: como o Peso, o registo nasce antes do Job On e
        // associa-se depois à mão (as punções não têm tool_id).
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4314", "Operador Punções", "provider-puncoes-4", "PuncoesPass4!");
        using var client = await LoginClientAsync("4314", "PuncoesPass4!");

        var start = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, PagePath));
        Assert.Contains(">Sem Job On</a>", start);
        Assert.Contains(">Associar Job On</a>", start);
        AssertModoNoCartao(start, "pu-escolha-title", jobOn: false);
        AssertModoNoCartao(WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon")), "pu-escolha-title", jobOn: true);
        Assert.DoesNotContain("+ Novo registo", start);

        var search = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?procura=1075c142"));
        Assert.Contains("Ainda não há registos sem Job On da referência 1075C142", search);
        Assert.Contains("name=\"referencia_total\" value=\"1075C142\"", search);
        Assert.Contains("+ Novo registo", search);
        // O «+ Novo registo» fica no canto superior direito do cartão, na
        // linha do título, e não no fundo por baixo da lista; envia o
        // formulário da referência e do lote (regra 100 do deep sweep de
        // 18/10/2026). Numa zona, para a procura sem recarregar o mostrar
        // (deep sweep 2, 18/10/2026, F-8).
        Assert.Matches("<nav class=\"dmo-modo\"[\\s\\S]*?</nav>\\s*<div id=\"puNovoAcoes\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div class=\"dmo-card-topo-acoes\"><button type=\"submit\" class=\"dmo-button\" form=\"pu-novo-form\">\\+ Novo registo</button></div>\\s*</div>\\s*</div>", search);
        var novoForm = System.Text.RegularExpressions.Regex.Match(search, "<form [^>]*id=\"pu-novo-form\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.Contains("handler=Create", novoForm);
        Assert.DoesNotContain("<button", novoForm);
        Assert.DoesNotContain("+ Novo registo", WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&procura=1075c142")));

        var refused = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "A"));
        Assert.Contains("recusa=InvalidLot", refused.Headers.Location!.ToString());

        var created = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "1"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var sheetPath = created.Headers.Location!.ToString().Split('#')[0];
        Assert.Contains("puncoes_id=", sheetPath);
        var puncoesId = sheetPath.Split("puncoes_id=")[1];

        var added = await PostFormAsync(client, sheetPath, "Add", ("puncoes_id", puncoesId), ("numero", "41"), ("referencia", "1075"), ("lote", "1"));
        Assert.DoesNotContain("recusa", added.Headers.Location!.ToString());

        var standalone = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, sheetPath));
        Assert.Contains("<span>Produção</span><strong>Sem Job On</strong>", standalone);
        Assert.Contains("<span>Total de punções</span><strong>1</strong>", standalone);
        Assert.Contains("Associar a uma produção", standalone);
        Assert.Contains(">202604 · C2</option>", standalone);

        var pdf = await PostFormAsync(client, sheetPath, "GeneratePdf", ("puncoes_id", puncoesId));
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        var listed = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?procura=1075C142"));
        // Sem botão «Abrir»: um clique na linha abre o registo.
        Assert.Contains($"data-dmo-open=\"/Controlo/Puncoes?puncoes_id={puncoesId}\"", listed);
        Assert.DoesNotContain(">Abrir</a>", listed);

        // A produção NNPB sem registo mostra o registo sem Job On para associar.
        var production = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("Associar · punção 1075 · lote 1", production);

        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            jobOnId = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().Controlos
                .Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
        }

        var associated = await PostFormAsync(client, sheetPath, "Associate", ("puncoes_id", puncoesId), ("jobon_id", jobOnId.ToString()));
        Assert.Equal($"/Controlo/Puncoes?controlo_id={controloId}#puncoes-lista", associated.Headers.Location!.ToString());

        var reopened = await client.GetAsync(sheetPath);
        Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
        Assert.Equal($"/Controlo/Puncoes?controlo_id={controloId}", reopened.Headers.Location!.ToString());

        var after = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("<span>Total de punções</span><strong>1</strong>", after);
        Assert.DoesNotContain("Associar · punção", after);

        // Um segundo registo sem Job On não entra na produção que já tem o seu.
        var second = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "1"));
        var secondId = second.Headers.Location!.ToString().Split('#')[0].Split("puncoes_id=")[1];
        var refusedAssociation = await PostFormAsync(client, $"{PagePath}?puncoes_id={secondId}", "Associate", ("puncoes_id", secondId), ("jobon_id", jobOnId.ToString()));
        Assert.Contains("recusa=ControloHasRegisto", refusedAssociation.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WithoutJobOn_TheAssociationSearchAndRefusals_ChangeOnlyTheirZone()
    {
        // Deep sweep, 18/10/2026 (n.º 105): a procura da referência e as
        // recusas do «Associar à produção» recarregavam a página toda. No
        // registo sem Job On, o cartão é uma zona (data-dmo-parcial): fora
        // das zonas a página é igual com qualquer procura ou recusa, e a
        // recusa volta com a referência procurada.
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4382", "Operador Punções", "provider-puncoes-20", "PuncoesPass20!");
        using var client = await LoginClientAsync("4382", "PuncoesPass20!");

        var created = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "1"));
        var sheetPath = created.Headers.Location!.ToString().Split('#')[0];
        var puncoesId = sheetPath.Split("puncoes_id=")[1];

        var sheet = await GetPageHtmlAsync(client, sheetPath);
        Assert.Contains("<div id=\"puAssociar\" class=\"dmo-zona\" data-dmo-regiao>", sheet);
        Assert.Contains("id=\"puncoes-associar\" aria-labelledby=\"pu-associar-title\" data-dmo-parcial>", sheet);
        Assert.Contains("<input type=\"hidden\" name=\"procura\" value=\"1075C142\" />", sheet);

        var fora = Controlo.PesoZonas.ForaDasZonas(sheet);
        foreach (var extra in new[]
        {
            "&procura=9999X1",
            "&procura=1075c142",
            "&procura=1075C142&jobon_recusa=inexistente",
            "&procura=9999X1&jobon_recusa=sem-escolha",
            "&recusa=ControloHasRegisto",
        })
        {
            var html = await GetPageHtmlAsync(client, sheetPath + extra);
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
        }

        Assert.Contains("Não há Job Ons da referência 9999X1",
            WebUtility.HtmlDecode(Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, sheetPath + "&procura=9999X1"), "puAssociar")));
        Assert.Contains("data-dmo-toast=\"erro\"",
            Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, sheetPath + "&procura=1075C142&jobon_recusa=inexistente"), "puAssociar"));

        // A recusa volta com a referência procurada; sem escolha também.
        var inexistente = await PostFormAsync(client, sheetPath, "Associate", ("puncoes_id", puncoesId), ("jobon_id", "999999"), ("procura", "1075c142"));
        Assert.Equal($"/Controlo/Puncoes?puncoes_id={puncoesId}&procura=1075C142&jobon_recusa=inexistente#puncoes-associar", inexistente.Headers.Location!.ToString());
        var semEscolha = await PostFormAsync(client, sheetPath, "Associate", ("puncoes_id", puncoesId), ("procura", "9999X1"));
        Assert.Equal($"/Controlo/Puncoes?puncoes_id={puncoesId}&procura=9999X1&jobon_recusa=sem-escolha#puncoes-associar", semEscolha.Headers.Location!.ToString());

        // A folha da produção não tem a zona: associado, a página abre inteira.
        Assert.DoesNotContain("id=\"puAssociar\"", await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
    }

    [Fact]
    public async Task Procura_TrocaSoAsZonas_ComAProducaoAoLadoDaReferencia()
    {
        // Deep sweep 2 (18/10/2026, F-8 e F-9): procurar a referência
        // recarregava a página inteira, e com Job On a produção ia para outra
        // linha. A referência fica (data-dmo-fica) e vai pelo DmoTroca; com
        // Job On a produção é uma zona ao lado dela, com o dropdown do
        // formulário POST (form=), que abre a folha como sempre
        // (data-dmo-nativo). Fora das zonas, a página é igual nas duas formas
        // em todas as procuras (com uma recusa também).
        await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4794", "Operador Punções", "provider-puncoes-8", "PuncoesPass8!");
        using var client = await LoginClientAsync("4794", "PuncoesPass8!");

        var jobOnVazio = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon");
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>\\s*<form [^>]*id=\"pu-procura-form\"", jobOnVazio);
        Assert.Contains("<div id=\"puProducaoZona\" class=\"dmo-field\" data-dmo-regiao hidden=\"hidden\">", jobOnVazio);
        Assert.Matches("<form [^>]*id=\"pu-jobon-form\" data-dmo-nativo[^>]*>", jobOnVazio);

        var jobOn = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&procura=1075C142"));
        var procura = System.Text.RegularExpressions.Regex.Match(jobOn, "<form [^>]*id=\"pu-procura-form\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.Contains("<select id=\"pu-producao\" name=\"jobon_id\" form=\"pu-jobon-form\" required data-dmo-autosubmit=\"valor\" data-dmo-depende=\"pu-procura\" data-dmo-depende-valor=\"1075C142\">", procura);
        Assert.Contains(">202604 · C2</option>", procura);

        var fora = Controlo.PesoZonas.ForaDasZonas(jobOnVazio);
        foreach (var filtros in new[] { "procura=1075C142", "procura=1075c142", "procura=9999X1", "jobon_recusa=sem-escolha", "procura=1075C142&jobon_recusa=inexistente" })
        {
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&{filtros}")));
        }
        Assert.Contains("Escolha a produção.", Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&jobon_recusa=sem-escolha"), "puEscolhaAvisos"));

        // Sem Job On: a lista dos registos e o «+ Novo registo» são zonas.
        var semJobOnVazio = await GetPageHtmlAsync(client, PagePath);
        Assert.DoesNotContain("id=\"puProducaoZona\"", semJobOnVazio);
        Assert.DoesNotContain("id=\"pu-jobon-form\"", semJobOnVazio);
        var foraSemJobOn = Controlo.PesoZonas.ForaDasZonas(semJobOnVazio);
        foreach (var filtros in new[] { "procura=1075C142", "procura=9999X1", "procura=1075C142&recusa=InvalidLot&referencia=1075&lote=A&referencia_total=1075C142" })
        {
            Assert.Equal(foraSemJobOn, Controlo.PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?{filtros}")));
        }
        var semJobOn = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?procura=1075C142"));
        Assert.Contains("+ Novo registo", Controlo.PesoZonas.Zona(semJobOn, "puNovoAcoes"));
        Assert.Contains("id=\"pu-novo-form\"", Controlo.PesoZonas.Zona(semJobOn, "puEscolhaResultados"));
        Assert.Contains("Nada foi registado.", Controlo.PesoZonas.Zona(WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?procura=1075C142&recusa=InvalidLot&referencia=1075&lote=A&referencia_total=1075C142")), "puEscolhaAvisos"));
    }

    [Fact]
    public async Task WithJobOn_TheProductionIsChosenInTwoSteps()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4315", "Operador Punções", "provider-puncoes-5", "PuncoesPass5!");
        using var client = await LoginClientAsync("4315", "PuncoesPass5!");

        var choose = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&procura=1075C142"));
        Assert.Contains(">202604 · C2</option>", choose);
        var none = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&procura=9999X1"));
        Assert.Contains("Não há Job Ons da referência 9999X1", none);

        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            jobOnId = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().Controlos
                .Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
        }

        var opened = await PostFormAsync(client, $"{PagePath}?modo=jobon&procura=1075C142", "AbrirJobOn", ("jobon_id", jobOnId.ToString()));
        Assert.Equal($"/Controlo/Puncoes?controlo_id={controloId}", opened.Headers.Location!.ToString());
        var missing = await PostFormAsync(client, $"{PagePath}?modo=jobon&procura=1075C142", "AbrirJobOn");
        Assert.Contains("jobon_recusa=sem-escolha", missing.Headers.Location!.ToString());

        var notFound = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id=2147483647"));
        Assert.Contains("O controlo indicado não existe", notFound);
        var registoNotFound = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?puncoes_id=2147483647"));
        Assert.Contains("O registo indicado não existe", registoNotFound);
    }

    /// <summary>A escolha do registo vive dentro do cartão e marca a opção ativa (Diogo, 17/10/2026).</summary>
    private static void AssertModoNoCartao(string html, string cardTitleId, bool jobOn)
    {
        var card = System.Text.RegularExpressions.Regex.Match(html,
            $"<section[^>]*aria-labelledby=\"{cardTitleId}\"[\\s\\S]*?</section>").Value;
        Assert.Contains("data-dmo-modo", card);
        Assert.Contains(jobOn ? "aria-current=\"page\">Associar Job On</a>" : "aria-current=\"page\">Sem Job On</a>", card);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-dmo-modo"));
    }
}

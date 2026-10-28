using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os dois Controlos como módulos (o Owner 2026-10-16: o template dá
/// módulos, cada módulo dá as suas páginas): o Controlo Criar tem o
/// Resumo, o Peso e os Pegamentos; o Controlo Aprovar tem a revisão e a
/// aprovação. Quem abre o Controlo que não tem entra no que tem, sem
/// correr nenhum handler. O ADMIN só administra.
/// </summary>
public sealed partial class ControloCapabilitiesCutHPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;

    public ControloCapabilitiesCutHPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    /// <summary>
    /// A base InMemory é PARTILHADA por todas as classes de teste do host
    /// (nome fixo); cada teste limpa o estado de domínio que tocou ao
    /// sair (dependentes ANTES dos principais — a ordem das FKs
    /// Restrict).
    /// </summary>
    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ControloViewings.RemoveRange(db.ControloViewings);
        db.ControloAreaStates.RemoveRange(db.ControloAreaStates);
        db.PegamentosMeasurements.RemoveRange(db.PegamentosMeasurements);
        db.Pegamentos.RemoveRange(db.Pegamentos);
        db.PegamentosTolerances.RemoveRange(db.PegamentosTolerances);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
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

    /// <summary>
    /// Semeia um user operacional COM O TEMPLATE dado — as permissões
    /// nascem dos users criados com os módulos (templates) atribuídos
    /// (o Owner 2026-10-12). Login por OperatorId (o caminho operacional).
    /// </summary>
    private void SeedUserWithTemplate(
        string operatorId,
        string name,
        string providerUserId,
        string password,
        string templateKey,
        string templateName,
        string[] capabilities)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == templateKey);
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = templateKey,
                Name = templateName,
                Capabilities = capabilities
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

        db.TemplateAssignments.Add(new TemplateAssignment
        {
            UserId = user.Id,
            TemplateId = template.Id
        });
        db.SaveChanges();
    }

    /// <summary>O CHEFE do corte H: approve sem create (o template do e724d9a).</summary>
    private void SeedChefe(string operatorId, string providerUserId, string password) =>
        SeedUserWithTemplate(
            operatorId,
            "Chefe Aprovação",
            providerUserId,
            password,
            "tpl-teste-chefe",
            "Chefe de aprovação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove]);

    /// <summary>O OPERACIONAL do corte H: create sem approve.</summary>
    private void SeedOperacional(string operatorId, string providerUserId, string password) =>
        SeedUserWithTemplate(
            operatorId,
            "Operacional Criação",
            providerUserId,
            password,
            "tpl-teste-operacional-h",
            "Operacional — criação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate]);

    /// <summary>
    /// Semeia o ADMIN pelo email das Definições (o padrão
    /// provado do AdminAuthorizationPolicyTests) — o ADMIN SÓ administra.
    /// </summary>
    private void SeedConfiguredAdmin(string email, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        dbContext.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = email
        });
        dbContext.SaveChanges();
    }

    /// <summary>
    /// Semeia a produção real (o J10 da base): B1, T3 (CM 5447 lote 3,
    /// NNPB), BQ-1 (BQ T194 12) e o MF-TOOL-9 (MF 5810 lote 1 — o molde
    /// final OBRIGATÓRIO) + a densidade do vidro do processo — o Job On
    /// pela porta canónica CreateJobOn, com o controlo na mesma escrita.
    /// </summary>
    private async Task<(int JobOnId, int ControloId)> SeedRealProductionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        await db.SaveChangesAsync();

        var createJobOn = scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
        var created = await createJobOn.HandleAsync(new CreateJobOn(
            "J10",
            "5810",
            machine.Id,
            "T3",
            "MF-TOOL-9",
            "BQ-1"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        var controloId = await db.Controlos.AsNoTracking()
            .Where(c => c.JobOnId == created.JobOn!.JobOnId)
            .Select(c => c.Id)
            .SingleAsync();
        return (created.JobOn!.JobOnId, controloId);
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password, WebApplicationFactory<Program>? factory = null)
    {
        var client = factory?.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }) ?? CreateClient();
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

    // ------------------------------------------------------------------
    // MOSTRAR TUDO: o esconder do modo-chefe (e724d9a) está revogado
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChefeApproveOnly_UsesDistinctReviewPages_WithoutOperationalForms()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        // Com um Peso registado, para haver o que aprovar.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(NovoPeso(controloId, null, null));
            await db.SaveChangesAsync();
        }
        SeedChefe("4700", "provider-chefe-h-1", "ChefeShowPass1!");
        using var client = await LoginClientAsync("4700", "ChefeShowPass1!");

        // Two independent destinations, selected via effective capabilities.
        var reviewHtml = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        // Ambos os destinos aparecem só como «Controlo»; o de revisão é o visível.
        Assert.Contains(">Controlo</a>", reviewHtml);
        Assert.Contains("href=\"/Controlo/Approve\"", reviewHtml);
        Assert.DoesNotContain("href=\"/Controlo/Resumo\"", reviewHtml);
        Assert.DoesNotContain("Controlo Approve", reviewHtml);
        Assert.DoesNotContain("Controlo Create", reviewHtml);
        Assert.DoesNotContain("Abrir revisão escolhida", reviewHtml);

        // A Revisão por calendário (17/10/2026): a página abre só com o
        // calendário; a lista nasce de um dia escolhido.
        var listHtml = await GetPageHtmlAsync(client, "/Controlo/Approve");
        Assert.Contains("Escolha um dia no calendário, ou use os filtros para consultar um período.", listHtml);
        Assert.DoesNotContain("class=\"revisao-item", listHtml);
        Assert.DoesNotContain("Abrir revisão escolhida", listHtml);
        Assert.Contains(">Aprovar</button>", reviewHtml);
        Assert.DoesNotContain(">Criar Peso</button>", reviewHtml);
        Assert.DoesNotContain(">Registar medição</button>", reviewHtml);
        Assert.Contains("data-user-profile-title", reviewHtml);
        Assert.Contains("Chefe de aprovação", reviewHtml);

        // As páginas do Controlo Criar não abrem para quem só tem o
        // Controlo Aprovar: a entrada leva-o ao seu Controlo, na mesma
        // produção quando o link a traz.
        using var pesoRedirect = await client.GetAsync($"/Controlo/Peso?controlo_id={controloId}");
        Assert.Equal(HttpStatusCode.Redirect, pesoRedirect.StatusCode);
        Assert.Equal("/Controlo/Approve", pesoRedirect.Headers.Location?.ToString());

        using var pegRedirect = await client.GetAsync($"/Controlo/Pegamentos?controlo_id={controloId}");
        Assert.Equal(HttpStatusCode.Redirect, pegRedirect.StatusCode);
        Assert.Equal("/Controlo/Approve", pegRedirect.Headers.Location?.ToString());

        using var resumoRedirect = await client.GetAsync($"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.Equal(HttpStatusCode.Redirect, resumoRedirect.StatusCode);
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", resumoRedirect.Headers.Location?.ToString());

        Assert.Contains("/Controlo/Approve/Detalhe?controlo_id=", reviewHtml);
        Assert.DoesNotContain(">Criar Peso</button>", reviewHtml);

        // Detail views are separate Razor pages: no operational write forms,
        // only the decision of the Peso; the Pegamentos have none (Diogo,
        // 19/10/2026).
        var detailHtml = await GetPageHtmlAsync(client,
            $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");
        Assert.Contains("Consulta dos Pegamentos", detailHtml);
        Assert.DoesNotContain("Registar medição", detailHtml);
        Assert.DoesNotContain("<form ", detailHtml);
        var pesoHtml = await GetPageHtmlAsync(client,
            $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        Assert.DoesNotContain(">Criar Peso</button>", pesoHtml);
        Assert.Single(Regex.Matches(pesoHtml, "<form "));
        Assert.Contains("<form method=\"post\" action=\"/Controlo/Approve?handler=Approve\"", pesoHtml);
    }

    [Theory]
    [InlineData(false, "abc", "nominal-ilegivel", "é ilegível")]
    [InlineData(true, "abc", "nominal-ilegivel", "é ilegível")]
    [InlineData(false, "200..50", "nominal-ilegivel", "é ilegível")]
    [InlineData(true, "200..50", "nominal-ilegivel", "é ilegível")]
    [InlineData(false, "0", "nominal-nao-positivo", "não é maior que 0")]
    [InlineData(true, "-5,5", "nominal-nao-positivo", "não é maior que 0")]
    public async Task PesoCreation_UnreadableOrNonPositiveOptionalNominal_CreatesWithoutNominalAndWarns(
        bool withProduction, string nominal, string aviso, string motivo)
    {
        // A5 (beta.65, regra do proprietário 16/10/2026: «tentar sempre
        // warning, desde que não parta nada»): o peso nominal é opcional —
        // escrito mas ilegível, ou ≤ 0, já não recusa o Peso. O Peso é criado
        // SEM nominal (nada inventado na Tool) e o aviso diz o que ficou de fora.
        var (_, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4781", "provider-nominal-draft", "NominalPass1!");
        using var client = await LoginClientAsync("4781", "NominalPass1!");
        var page = withProduction ? $"/Controlo/Peso?controlo_id={controloId}"
            : "/Controlo/Peso?cm_tool_id=T3&bq_tool_id=BQ-1";
        var handler = withProduction ? "CreateForControlo" : "Create";

        using var created = await client.PostAsync(
            $"/Controlo/Peso?handler={handler}", HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()), ("cm_tool_id", "T3"), ("bq_tool_id", "BQ-1"),
                ("water_temperature", "20.4"), ("cm_volume_puncao", "30.00"),
                ("bq_volume_marisa", "10.00"), ("cm_nominal_weight", nominal),
                ("__RequestVerificationToken", await GetVerificationTokenAsync(client, page))));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        Assert.DoesNotContain("InvalidNominalWeight", location);
        Assert.DoesNotContain("InvalidTechnicalValue", location);
        Assert.Contains($"aviso={aviso}", location);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("peso-aviso warn", html);
        Assert.Contains($"O peso nominal escrito («{nominal}») {motivo}: o Peso foi criado sem ele.", html);
        Assert.Contains("200,00 ou 200.00", html);

        using var saved = _factory.Services.CreateScope();
        var database = saved.ServiceProvider.GetRequiredService<DmoDbContext>();
        var peso = Assert.Single(database.Pesos);
        Assert.Null(peso.NominalWeightGrams);
        Assert.Equal(withProduction ? (int?)controloId : null, peso.ControloId);
        Assert.Null(database.ToolTechnicalContexts.Single(t => t.ToolId == "T3").NominalWeightGrams);
        Assert.Equal(30m, peso.VolumePuncaoCm3);
        Assert.Equal(10m, peso.VolumeMarisaCm3);
        Assert.Single(database.Controlos);
    }

    [Fact]
    public async Task PesoCreation_ReadableNominal_NoWarning()
    {
        // O caminho normal não ganha aviso: um nominal legível grava-se.
        var (_, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4782", "provider-nominal-ok", "NominalPass2!");
        using var client = await LoginClientAsync("4782", "NominalPass2!");
        var page = $"/Controlo/Peso?controlo_id={controloId}";

        using var created = await client.PostAsync(
            "/Controlo/Peso?handler=CreateForControlo", HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()),
                ("water_temperature", "20,4"), ("cm_volume_puncao", "30,00"),
                ("bq_volume_marisa", "10.00"), ("cm_nominal_weight", "200,50"),
                ("__RequestVerificationToken", await GetVerificationTokenAsync(client, page))));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        Assert.DoesNotContain("aviso=", location);
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.DoesNotContain("é ilegível: o Peso foi criado sem ele", html);

        using var saved = _factory.Services.CreateScope();
        var database = saved.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(200.50m, Assert.Single(database.Pesos).NominalWeightGrams);
    }

    [Theory]
    [InlineData("peso")]
    [InlineData("pegamentos")]
    public async Task ReviewTab_ReturnsToTheExplicitProduction(string tipo)
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var create = scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
            var other = await create.HandleAsync(new CreateJobOn(
                "J20", "5810", db.Machines.Single().Id, "T3", "MF-TOOL-9", "BQ-1"));
            Assert.Equal(CreateJobOnResult.Success, other.Result);
        }
        SeedChefe("4780", "provider-review-navigation", "ReviewPass1!");
        using var client = await LoginClientAsync("4780", "ReviewPass1!");

        static string ReviewLink(string html)
        {
            var nav = Regex.Match(html, "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
            return WebUtility.HtmlDecode(Regex.Match(nav, "href=\"([^\"]*)\"").Groups[1].Value);
        }

        var detail = await GetPageHtmlAsync(client,
            $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}");
        var link = ReviewLink(detail);
        Assert.Contains($"jobon_id={jobOnId}", link);
        var review = await GetPageHtmlAsync(client, link);
        Assert.Contains("<div><span>Referência</span><strong>5810</strong></div>", review);
        Assert.Contains("<div><span>Produção</span><strong>J10</strong></div>", review);
        var reopened = await GetPageHtmlAsync(client, ReviewLink(review));
        Assert.Contains("<div><span>Produção</span><strong>J10</strong></div>", reopened);

        foreach (var path in new[] { "/Controlo/Approve", "/Controlo/Approve?jobon_id=2147483647",
            "/Controlo/Approve/Detalhe", "/Controlo/Approve/Detalhe?controlo_id=2147483647" })
        {
            var empty = await GetPageHtmlAsync(client, path);
            Assert.Equal("/Controlo/Approve", ReviewLink(empty));
            Assert.DoesNotContain("id=\"approve-production-title\"", empty);
        }
        using var check = _factory.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(2, saved.Controlos.Count());
        Assert.Empty(saved.Pesos);
        Assert.Empty(saved.Pegamentos);
        Assert.Empty(saved.ControloViewings);
    }

    // ------------------------------------------------------------------
    // OS GATES MANTÊM-SE: recusas tipadas, contexto preservado
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChefeApproveOnly_CreateRefusedTyped_ApproveSucceeds()
    {
        // O template guarda os ATOS: o chefe (approve sem create) que
        // submete uma medição de Pegamentos recebe a RECUSA TIPADA —
        // mensagem limpa por query (PRG), a folha preservada, NADA
        // escrito; o APPROVE dele passa o gate e sela (o lado positivo:
        // o gate não bloqueia quem tem a capability).
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedChefe("4701", "provider-chefe-h-2", "ChefeGatePass1!");
        using var client = await LoginClientAsync("4701", "ChefeGatePass1!");

        // O POST de medição recusado TIPODO (a capability em falta no
        // template — o ato é do create).
        // Approve-only pages have no write form; get the token from the chief's
        // authorized Resumo approval form to test direct POST backend denial.
        var token = await GetVerificationTokenAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        var refused = await client.PostAsync(
            "/Controlo/Pegamentos?handler=RegisterMeasurement",
            HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()),
                ("component", "CM"),
                ("costura", "70.10"),
                ("gargalo", "70.00"),
                ("__RequestVerificationToken", token)));
        // A entrada do módulo recusa o POST antes do handler: nada é
        // escrito e o chefe volta ao seu Controlo.
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Equal("/Controlo/Approve", refused.Headers.Location?.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Pegamentos.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.PegamentosMeasurements.AsNoTracking().CountAsync());
        }

        // O APPROVE do chefe PASSA o gate: o selo fica gravado com o
        // SEU operador (o ato é do approve).
        var approveToken = await GetVerificationTokenAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        var approved = await client.PostAsync(
            "/Controlo/Approve?handler=Approve",
            HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()),
                ("__RequestVerificationToken", approveToken)));
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", approved.Headers.Location?.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal("4701", controlo.ApprovedByOperatorId);
        }
    }

    [Fact]
    public async Task Review_OpeningWritesNothing_ApprovingCreatesTheMissingControlo()
    {
        // Uma produção antiga sem controlo: abrir na Revisão só lê (varredura
        // de 18/10/2026, C5); aprovar cria o controlo e grava a aprovação.
        var (jobOnId, _) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Controlos.RemoveRange(db.Controlos);
            await db.SaveChangesAsync();
        }

        SeedChefe("4702", "provider-chefe-h-3", "ChefeGatePass2!");
        using var client = await LoginClientAsync("4702", "ChefeGatePass2!");

        var token = await GetVerificationTokenAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Controlos.AsNoTracking().CountAsync());
        }

        var approved = await client.PostAsync(
            "/Controlo/Approve?handler=Approve",
            HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal(jobOnId, controlo.JobOnId);
            Assert.Equal("4702", controlo.ApprovedByOperatorId);
        }
    }

    [Fact]
    public async Task AreaState_OperacionalSavesOkNokAndComment_ChefeIsRefusedTyped()
    {
        // O ESTADO DO DOCUMENTO: o operacional (create) grava OK/NOK e o
        // comentário de uma área; a folha reabre com eles. O chefe
        // (approve) recebe a recusa tipada e nada muda.
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4704", "provider-oper-h-area", "OperAreaPass1!");
        SeedChefe("4705", "provider-chefe-h-area", "ChefeAreaPass1!");

        using (var operacional = await LoginClientAsync("4704", "OperAreaPass1!"))
        {
            var token = await GetVerificationTokenAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}");
            var saved = await operacional.PostAsync(
                "/Controlo/Resumo?handler=AreaState",
                HtmlFormHelpers.BuildFormPost(
                    ("jobon_id", jobOnId.ToString()),
                    ("area", "BQ"),
                    ("estado", "NOK"),
                    ("comentario", "Boquilha com marca no gargalo"),
                    ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.DoesNotContain("capability_refusal", saved.Headers.Location?.ToString(), StringComparison.Ordinal);

            var sheet = await operacional.GetStringAsync($"/Controlo/Resumo?jobon_id={jobOnId}");
            Assert.Contains("Boquilha com marca no gargalo", sheet, StringComparison.Ordinal);
            Assert.Contains("resumo-area nok", sheet, StringComparison.Ordinal);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var state = await db.ControloAreaStates.AsNoTracking().SingleAsync(s => s.ControloId == controloId);
            Assert.Equal(("BQ", "NOK", "4704"), (state.Area, state.Status, state.UpdatedBy));
        }

        using var chefe = await LoginClientAsync("4705", "ChefeAreaPass1!");
        var chefeToken = await GetVerificationTokenAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
        var refused = await chefe.PostAsync(
            "/Controlo/Resumo?handler=AreaState",
            HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()),
                ("area", "BQ"),
                ("estado", "OK"),
                ("__RequestVerificationToken", chefeToken)));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Equal("/Controlo/Approve", refused.Headers.Location?.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal("NOK", (await db.ControloAreaStates.AsNoTracking().SingleAsync(s => s.ControloId == controloId)).Status);
        }
    }

    [Fact]
    public async Task McaliperLink_CreateAssociatesOnToolSheet_ApproveCopiesIt()
    {
        // O LINK DO MCALIPER (Diogo, 17/10/2026): o operacional associa-o na
        // área BQ do Resumo; fica na ficha técnica da Tool da BQ (que ainda
        // não tinha linha técnica) e nunca aparece escrito. Na Revisão, o
        // chefe tem o ícone que copia. Um endereço que não é http(s) é recusado.
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedOperacional("4706", "provider-oper-h-mcal", "OperMcalPass1!");
        SeedChefe("4707", "provider-chefe-h-mcal", "ChefeMcalPass1!");
        const string url = "https://mcaliper.example/medicoes/T194-12";

        using (var operacional = await LoginClientAsync("4706", "OperMcalPass1!"))
        {
            var sheetBefore = await operacional.GetStringAsync($"/Controlo/Resumo?jobon_id={jobOnId}");
            // Sem link, quem tem o Criar cola-o num campo da linha do Resumo (DMO-524, A-0254).
            Assert.Contains("aria-label=\"Link do mcaliper BQ\"", sheetBefore, StringComparison.Ordinal);
            Assert.DoesNotContain("data-resumo-mcaliper-abrir=\"BQ\"", sheetBefore, StringComparison.Ordinal);

            var token = await GetVerificationTokenAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}");
            var refused = await operacional.PostAsync(
                "/Controlo/Resumo?handler=Mcaliper",
                HtmlFormHelpers.BuildFormPost(
                    ("jobon_id", jobOnId.ToString()), ("area", "BQ"), ("url", "javascript:alert(1)"),
                    ("__RequestVerificationToken", token)));
            Assert.Contains("mcaliper=invalido", refused.Headers.Location?.ToString(), StringComparison.Ordinal);

            var saved = await operacional.PostAsync(
                "/Controlo/Resumo?handler=Mcaliper",
                HtmlFormHelpers.BuildFormPost(
                    ("jobon_id", jobOnId.ToString()), ("area", "BQ"), ("url", "  " + url + " "),
                    ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.DoesNotContain("mcaliper=", saved.Headers.Location?.ToString(), StringComparison.Ordinal);

            var sheet = await operacional.GetStringAsync($"/Controlo/Resumo?jobon_id={jobOnId}");
            Assert.Contains("data-resumo-mcaliper-abrir=\"BQ\">Medições</a>", sheet, StringComparison.Ordinal);
            Assert.Contains($"href=\"{url}\"", sheet, StringComparison.Ordinal);
            // O campo fica com o link gravado, para o mudar.
            Assert.Contains($"name=\"url\" value=\"{url}\"", sheet, StringComparison.Ordinal);

            // O campo grava ao sair (data-dmo-valor): o pedido JSON recebe { ok, valor, aviso }.
            async Task<string> GravarNoCampoAsync(string valor)
            {
                using var pedido = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Resumo?handler=Mcaliper")
                {
                    Content = HtmlFormHelpers.BuildFormPost(
                        ("jobon_id", jobOnId.ToString()), ("area", "BQ"), ("url", valor),
                        ("__RequestVerificationToken", token)),
                };
                pedido.Headers.Accept.ParseAdd("application/json");
                using var resposta = await operacional.SendAsync(pedido);
                Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
                return await resposta.Content.ReadAsStringAsync();
            }
            var recusadoNoCampo = await GravarNoCampoAsync("ftp://mcaliper");
            Assert.Contains("\"ok\":false", recusadoNoCampo, StringComparison.Ordinal);
            Assert.Contains("http://", recusadoNoCampo, StringComparison.Ordinal);
            var gravadoNoCampo = await GravarNoCampoAsync(" " + url + " ");
            Assert.Contains("\"ok\":true", gravadoNoCampo, StringComparison.Ordinal);
            Assert.Contains($"\"valor\":\"{url}\"", gravadoNoCampo, StringComparison.Ordinal);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(url, (await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "BQ-1")).McaliperUrl);
            Assert.Null((await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "T3")).McaliperUrl);
        }

        using var chefe = await LoginClientAsync("4707", "ChefeMcalPass1!");
        var review = System.Net.WebUtility.HtmlDecode(await chefe.GetStringAsync($"/Controlo/Approve?jobon_id={jobOnId}"));
        Assert.Contains($"data-dmo-copiar=\"{url}\"", review, StringComparison.Ordinal);
        Assert.Contains("Copiar o link do mcaliper da BQ", review, StringComparison.Ordinal);
        Assert.DoesNotContain("da CM\"", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PesoAssociation_PrePesoWithJobOnCm_IsOfferedAndAssociatedOnlyByExplicitCreatePost()
    {
        // O Owner 2026-10-16: «quando existir um jobon com CM_ID que usa o
        // tool_id perguntar se quero associar». Um Peso pré-produção da CM
        // T3 (a CM do J10) é proposto no Peso aberto, no Resumo e na folha
        // de Peso da produção; só o POST explícito de quem tem
        // controlo.create associa — o MESMO peso_id.
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4712", "provider-oper-assoc", "OperAssoc1!");
        SeedChefe("4713", "provider-chefe-assoc", "ChefeAssoc1!");

        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-1",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 70m,
                GlassDensityGramsPerCm3 = 2.50m,
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3"
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesoId = peso.Id;
        }

        using (var chefe = await LoginClientAsync("4713", "ChefeAssoc1!"))
        {
            var token = await GetVerificationTokenAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
            var refused = await chefe.PostAsync(
                "/Controlo/Peso?handler=Associate",
                HtmlFormHelpers.BuildFormPost(
                    ("peso_id", pesoId.ToString()),
                    ("jobon_id", jobOnId.ToString()),
                    ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/Controlo/Approve", refused.Headers.Location?.ToString());
        }

        using var operacional = await LoginClientAsync("4712", "OperAssoc1!");

        var openPeso = await GetPageHtmlAsync(operacional, $"/Controlo/Peso?peso_id={pesoId}");
        Assert.Contains("Quer associar este Peso?", openPeso, StringComparison.Ordinal);
        Assert.Contains("Produção J10", openPeso, StringComparison.Ordinal);

        var resumo = await GetPageHtmlAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.Contains("Associar Peso existente", resumo, StringComparison.Ordinal);
        // Os botões do cartão do Peso no Resumo ficam no canto superior
        // direito, na linha do título, e não no fundo (regra 60 do deep sweep
        // de 18/10/2026).
        Assert.Matches("<div class=\"resumo-linked-card-head\">\\s*<span>Peso</span>\\s*<div class=\"dmo-card-topo-acoes resumo-card-actions\">\\s*"
            + "<span class=\"resumo-status\">Sem registo</span>\\s*"
            + "<a class=\"dmo-button\" href=\"/Controlo/Peso\\?controlo_id=\\d+#peso-associate-title\">Associar Peso existente</a>\\s*"
            + "<a class=\"dmo-button\" href=\"/Controlo/Peso\\?controlo_id=\\d+\" aria-label=\"Registar o Peso\">Registar</a>\\s*</div>\\s*</div>", resumo);
        Assert.Matches("<span>Pegamentos</span>\\s*<div class=\"dmo-card-topo-acoes resumo-card-actions\">\\s*"
            + "<span class=\"resumo-status\">Sem registo</span>\\s*<a class=\"dmo-button\"[^>]*aria-label=\"Registar os Pegamentos\">Registar</a>\\s*</div>", resumo);
        Assert.Single(Regex.Matches(resumo, "aria-label=\"Registar o Peso\">Registar</a>"));

        var productionPeso = await GetPageHtmlAsync(operacional, $"/Controlo/Peso?controlo_id={controloId}");
        Assert.Contains("Associar a esta produção", productionPeso, StringComparison.Ordinal);
        // «Pesos sem produção»: a tabela sem botões (o Owner 2026-10-12) e um
        // só «Associar a esta produção» no canto do cartão, que envia a linha
        // escolhida (regra 79 do deep sweep de 18/10/2026). Com um só Peso, a
        // linha já vem escolhida.
        var associarCartao = Regex.Match(productionPeso,
            "<section class=\"dmo-card peso-card\" aria-labelledby=\"peso-associate-title\"[\\s\\S]*?</section>").Value;
        Assert.NotEmpty(associarCartao);
        Assert.Matches("<div class=\"dmo-card-topo-acoes\" data-dmo-for=\"pesoAssociateList\"><button type=\"submit\" class=\"dmo-button\" form=\"pesoAssociateForm\">Associar a esta produção</button></div>\\s*</div>\\s*"
            + "<form method=\"post\" id=\"pesoAssociateForm\" action=\"/Controlo/Peso\\?handler=Associate\">", associarCartao);
        Assert.Single(Regex.Matches(associarCartao, "<button"));
        var tabela = Regex.Match(associarCartao, "<table[\\s\\S]*?</table>").Value;
        Assert.DoesNotContain("<button", tabela);
        Assert.DoesNotContain("<form", tabela);
        Assert.Contains("<table class=\"dmo-table\" id=\"pesoAssociateList\"", tabela);
        Assert.Matches($"<tr data-dmo-row data-id=\"{pesoId}\" data-dmo-open=\"/Controlo/Peso\\?peso_id={pesoId}\"[^>]*class=\"selected\"", tabela);
        Assert.Matches($"<input type=\"radio\" name=\"peso_id\" value=\"{pesoId}\" required checked=\"checked\"", tabela);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Null((await db.Pesos.AsNoTracking().SingleAsync()).ControloId);
        }

        var associateToken = HtmlFormHelpers.ExtractRequestVerificationToken(openPeso)!;
        var associated = await operacional.PostAsync(
            "/Controlo/Peso?handler=Associate",
            HtmlFormHelpers.BuildFormPost(
                ("peso_id", pesoId.ToString()),
                ("jobon_id", jobOnId.ToString()),
                ("__RequestVerificationToken", associateToken)));
        Assert.Equal(HttpStatusCode.Redirect, associated.StatusCode);
        Assert.Contains($"controlo_id={controloId}", associated.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Contains("associado=1", associated.Headers.Location?.ToString(), StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var stored = await db.Pesos.AsNoTracking().SingleAsync();
            Assert.Equal(pesoId, stored.Id);
            Assert.Equal(controloId, stored.ControloId);
        }

        var resumoAfter = await GetPageHtmlAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.DoesNotContain("Associar Peso existente", resumoAfter, StringComparison.Ordinal);
        Assert.Contains("Registado", resumoAfter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PesosSemProducao_TwoCandidates_ChooseTheRowAndAssociateFromTheCorner()
    {
        // Regra 79 do deep sweep de 18/10/2026: nas tabelas não há botões (o
        // Owner 2026-10-12). Com dois Pesos sem produção, nenhuma linha vem
        // escolhida; o rádio escondido (peso_id) é obrigatório e o único
        // «Associar a esta produção» do canto envia a linha escolhida, com o
        // controlo_id do formulário.
        var (_, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4714", "provider-oper-assoc-2", "OperAssoc2!");

        var pesoIds = new List<int>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            foreach (var lote in new[] { "3", "4" })
            {
                var peso = new Peso
                {
                    CmToolId = "T3",
                    BqToolId = "BQ-1",
                    Process = "NNPB",
                    WaterTemperatureC = 20,
                    VolumeMarisaCm3 = 10m,
                    VolumePuncaoCm3 = 70m,
                    GlassDensityGramsPerCm3 = 2.50m,
                    CmToolReferenceFrozen = "5447",
                    CmToolLotFrozen = lote
                };
                db.Pesos.Add(peso);
                await db.SaveChangesAsync();
                pesoIds.Add(peso.Id);
            }
        }

        using var operacional = await LoginClientAsync("4714", "OperAssoc2!");
        var html = await GetPageHtmlAsync(operacional, $"/Controlo/Peso?controlo_id={controloId}");
        var form = Regex.Match(html, "<form method=\"post\" id=\"pesoAssociateForm\" action=\"/Controlo/Peso\\?handler=Associate\">[\\s\\S]*?</form>").Value;
        Assert.NotEmpty(form);
        Assert.Contains($"name=\"controlo_id\" value=\"{controloId}\"", form);
        Assert.DoesNotContain("<button", form);
        Assert.DoesNotContain("checked", form);
        Assert.DoesNotContain("class=\"selected\"", form);
        foreach (var id in pesoIds)
            Assert.Contains($"<input type=\"radio\" name=\"peso_id\" value=\"{id}\" required aria-label=\"Peso {id}\" />", form);
        Assert.Single(Regex.Matches(html, "form=\"pesoAssociateForm\""));

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var associated = await operacional.PostAsync(
            "/Controlo/Peso?handler=Associate",
            HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()),
                ("peso_id", pesoIds[1].ToString()),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, associated.StatusCode);
        Assert.Equal($"/Controlo/Peso?controlo_id={controloId}&associado=1", associated.Headers.Location?.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var stored = await db.Pesos.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
            Assert.Null(stored.Single(p => p.Id == pesoIds[0]).ControloId);
            Assert.Equal(controloId, stored.Single(p => p.Id == pesoIds[1]).ControloId);
        }
    }

    [Fact]
    public async Task PesoGates_CreatePassesForCapable_ReadingRefusedTypedForChefe()
    {
        // Os DOIS gates do Peso: o operacional (create) cria o Peso da
        // produção pelo POST (o gate passa para quem tem a capability);
        // o chefe (approve) que submete uma LEITURA recebe a recusa
        // tipada — a folha permanece, nada é escrito.
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4702", "provider-oper-h-1", "OperHPass1!");
        SeedChefe("4703", "provider-chefe-h-3", "ChefeHPass1!");

        // O OPERACIONAL cria o Peso da produção (o gate passa; o form
        // completo em falta: punção da CM e marisa da BQ — regra
        // Ferramentas: gravam no tool_id owner).
        using (var operacional = await LoginClientAsync("4702", "OperHPass1!"))
        {
            var createToken = await GetVerificationTokenAsync(operacional, $"/Controlo/Peso?controlo_id={controloId}");
            var created = await operacional.PostAsync(
                "/Controlo/Peso?handler=CreateForControlo",
                HtmlFormHelpers.BuildFormPost(
                    ("controlo_id", controloId.ToString()),
                    ("water_temperature", "20.5"),
                    ("cm_volume_puncao", "70.00"),
                    ("bq_volume_marisa", "10.00"),
                    ("__RequestVerificationToken", createToken)));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.DoesNotContain("capability_refusal", created.Headers.Location?.ToString(), StringComparison.Ordinal);
        }

        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = await db.Pesos.AsNoTracking().SingleAsync();
            pesoId = peso.Id;
        }

        // O CHEFE não pode registar a leitura: recusa tipada «leitura»,
        // a folha do Peso preservada, NENHUMA leitura escrita.
        using (var chefe = await LoginClientAsync("4703", "ChefeHPass1!"))
        {
            var readingToken = await GetVerificationTokenAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
            var refused = await chefe.PostAsync(
                "/Controlo/Peso?handler=RegisterMeasurement",
                HtmlFormHelpers.BuildFormPost(
                    ("peso_id", pesoId.ToString()),
                    ("cm_subject", "12"),
                    ("water_weight", "199.64"),
                    ("__RequestVerificationToken", readingToken)));
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/Controlo/Approve", refused.Headers.Location?.ToString());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.PesoMeasurements.AsNoTracking().CountAsync());
        }
    }

    // ------------------------------------------------------------------
    // A REVOGAÇÃO: o admin SÓ administra (o «fix» 7990275 revogado)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Admin_OperationalRoutesAreBlockedOnGetAndPost_AdminAreaRemainsOpen()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedConfiguredAdmin("cuteh-admin@example.test", "provider-cuteh-admin", "CuteHAdmin123!");
        using var admin = await LoginClientAsync("cuteh-admin@example.test", "CuteHAdmin123!");

        var entry = await admin.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);

        foreach (var path in new[] { "/JobOn", "/Controlo/Peso", "/Boquilhas", "/Ferramentas" })
        {
            var refusedGet = await admin.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refusedGet.StatusCode);
            Assert.Equal("/Admin/Users", refusedGet.Headers.Location?.OriginalString);
        }

        var refused = await admin.PostAsync(
            "/Controlo/Resumo?handler=Approve",
            HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString())));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var toolPost = await admin.PostAsync("/Ferramentas?handler=Create",
            HtmlFormHelpers.BuildFormPost(("reference", "forbidden")));
        Assert.Equal(HttpStatusCode.Forbidden, toolPost.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Null((await db.Controlos.AsNoTracking().SingleAsync()).ApprovedByOperatorId);
    }

    // ------------------------------------------------------------------
    // APROVAR: avisos em vez de bloqueios (Owner 16/10/2026)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Approve_WithoutPeso_ShowsNeutralNotice_AndApproves()
    {
        // O cartão já não diz «Não é possível aprovar um Peso inexistente»
        // (o botão funciona): é um aviso neutro e a aprovação grava.
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedChefe("4706", "provider-chefe-h-sem-peso", "ChefeSemPeso1!");
        using var client = await LoginClientAsync("4706", "ChefeSemPeso1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.DoesNotContain("Não é possível aprovar um Peso inexistente", html);
        Assert.Contains("Ainda não há Peso registado nesta produção.", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var approved = await client.PostAsync("/Controlo/Approve?handler=Approve",
            HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", approved.Headers.Location?.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("4706", (await db.Controlos.AsNoTracking().SingleAsync()).ApprovedByOperatorId);
    }

    [Fact]
    public async Task Approve_WithoutIdentityClaim_IsRefusedWithMessage_NotSilently()
    {
        // Sem identidade na sessão não há registo de quem aprovou: a
        // aprovação continua recusada, mas a página di-lo (antes: silêncio).
        var (jobOnId, _) = await SeedRealProductionAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var model = ActivatorUtilities.CreateInstance<DMO.Alpha.Web.Pages.Controlo.ApproveModel>(scope.ServiceProvider);
            model.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider,
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(DMO.Alpha.Web.Services.DmoClaimTypes.OperatorId, " ")],
                        "test"))
                }
            };

            var result = await model.OnPostApproveAsync(jobOnId);
            var redirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(result);
            Assert.Equal("/Controlo/Approve", redirect.PageName);
            Assert.Equal("sem-identidade", redirect.RouteValues!["aprovacao_recusada"]);
            Assert.Equal(jobOnId, redirect.RouteValues["jobon_id"]);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Null((await db.Controlos.AsNoTracking().SingleAsync()).ApprovedByOperatorId);
        }

        SeedChefe("4707", "provider-chefe-h-sem-id", "ChefeSemId1!");
        using var client = await LoginClientAsync("4707", "ChefeSemId1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}&aprovacao_recusada=sem-identidade");
        Assert.Contains("A aprovação não foi registada: a sessão não identifica o operador", html);
        var plain = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.DoesNotContain("A aprovação não foi registada", plain);
    }
    private sealed class ListOnlyReviewQuery(IControloRevisaoQuery inner) : IControloRevisaoQuery
    {
        public Task<IReadOnlyList<ControloRevisaoItem>> DayAsync(DateOnly day, string? procura, ControloRevisaoEstado? estado, string? maquina = null, CancellationToken cancellationToken = default) =>
            inner.DayAsync(day, procura, estado, maquina, cancellationToken);
        public Task<IReadOnlyList<ControloRevisaoItem>> PeriodAsync(DateOnly from, DateOnly toExclusive, string? procura, ControloRevisaoEstado? estado, string? maquina, int max, CancellationToken cancellationToken = default) =>
            inner.PeriodAsync(from, toExclusive, procura, estado, maquina, max, cancellationToken);
        public Task<IReadOnlyList<ControloRevisaoItem>> UndatedAsync(string? procura, ControloRevisaoEstado? estado, string? maquina = null, CancellationToken cancellationToken = default) =>
            inner.UndatedAsync(procura, estado, maquina, cancellationToken);
        public Task<ControloRevisaoItem?> FindByJobOnAsync(int jobOnId, CancellationToken cancellationToken = default) =>
            inner.FindByJobOnAsync(jobOnId, cancellationToken);
        public Task<IReadOnlyList<ControloRevisaoDia>> MonthAsync(int year, int month, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("List-only request loaded the calendar");
        public Task<int> CountUndatedPendingAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("List-only request loaded the undated counter");
        public Task<ControloRevisaoItem?> FindPreviousWithPesoAsync(int jobOnId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("List-only request loaded detail");
        public Task<string?> OperatorNameAsync(string operatorId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("List-only request loaded the decision");
    }

    [Fact]
    public async Task Revisao_ResultadosParciais_RespeitamFiltrosContextoEAcesso_SemCalendarioOuDetalhe()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        DateTime registered;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            registered = Assert.IsType<DateTime>(peso.RegistadoEmUtc);
        }
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IControloRevisaoQuery>(sp => new ListOnlyReviewQuery(
                new DMO.Alpha.Infrastructure.Controlo.ControloRevisaoQuery(sp.GetRequiredService<DmoDbContext>())))));
        using var anonymous = CreateClient();
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync("/Controlo/Approve?handler=Resultados&sem_data=1")).StatusCode);
        SeedChefe("4791", "provider-revisao-parcial", "RevisaoParcial1!");
        using var client = await LoginClientAsync("4791", "RevisaoParcial1!", factory);
        var local = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(registered);
        var query = $"jobon_id={jobOnId}&ano={local.Year}&mes={local.Month}&dia={local.Day}&procura=%205447%20&estado=pendente";
        var response = await client.GetAsync("/Controlo/Approve?handler=Resultados&" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("revisaoResultados", response.Headers.GetValues("X-Dmo-Region").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"revisaoResultados\" data-dmo-regiao", html);
        Assert.Contains("aria-current=\"true\"", html);
        Assert.Contains("procura=5447", html);
        Assert.Contains("estado=pendente", html);
        Assert.Contains($"jobon_id={jobOnId}", html);
        Assert.DoesNotContain("handler=Resultados", html);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dmo-calendar", html);
        Assert.DoesNotContain("revisaoDetalhe", html);
        Assert.DoesNotContain("<form", html);
        var empty = await client.GetStringAsync("/Controlo/Approve?handler=Resultados&" + query.Replace("estado=pendente", "estado=aprovado"));
        Assert.Contains("Nenhum controlo com estes filtros.", empty);
        var inferred = await client.GetStringAsync($"/Controlo/Approve?handler=Resultados&jobon_id={jobOnId}");
        Assert.Contains("aria-current=\"true\"", inferred);
        var invalid = await client.GetStringAsync("/Controlo/Approve?handler=Resultados&ano=2026&mes=2&dia=31&estado=desconhecido");
        Assert.DoesNotContain("class=\"revisao-item", invalid);
        var undated = await client.GetStringAsync("/Controlo/Approve?handler=Resultados&sem_data=1&procura=INEXISTENTE");
        Assert.Contains("Nenhum controlo com estes filtros.", undated);
        using var check = _factory.Services.CreateScope();
        var control = await check.ServiceProvider.GetRequiredService<DmoDbContext>().Controlos.AsNoTracking().SingleAsync();
        Assert.Null(control.ApprovedByOperatorId);
        Assert.Null(control.NotApprovedByOperatorId);
    }

    [Fact]
    public async Task Revisao_Calendario_MarcaODiaDoPeso_E_NaoAprovarExigeNota()
    {
        // A Revisão por calendário (17/10/2026): o Peso gravado fica com a
        // data do registo, o dia dele acende no calendário e a lista do dia
        // abre o controlo; «Não aprovar» sem nota não grava nada.
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        DateTime registadoEm;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-1",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 70m,
                GlassDensityGramsPerCm3 = 2.50m,
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3",
                ControloId = controloId
            };
            peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "12", WaterWeightGrams = 150m });
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            registadoEm = Assert.IsType<DateTime>(peso.RegistadoEmUtc);
        }

        SeedChefe("4790", "provider-chefe-revisao-cal", "ChefeRevisao1!");
        using var client = await LoginClientAsync("4790", "ChefeRevisao1!");

        var local = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(registadoEm);
        var dia = await GetPageHtmlAsync(client, $"/Controlo/Approve?ano={local.Year}&mes={local.Month}&dia={local.Day}");
        Assert.Contains($"Controlos de {local:dd/MM/yyyy}", dia);
        Assert.Contains("class=\"revisao-item mch-", dia);
        Assert.Contains($"jobon_id={jobOnId}", dia);
        Assert.Contains("dmo-calendar-day has selected", dia);

        // O controlo aberto leva o calendário ao dia do registo.
        var aberto = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains($"Controlos de {local:dd/MM/yyyy}", aberto);
        // A comparação passou para a Consulta do Peso (Diogo, 19/10/2026);
        // na Revisão fica o resumo e um Aprovar / Não aprovar por parte.
        Assert.DoesNotContain("Comparação global de peso", aberto);
        Assert.Contains("<span>Delta ao controlo anterior</span>", aberto);
        Assert.Contains("id=\"revisaoNaoAprovar-peso\"", aberto);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(aberto)!;
        var semNota = await client.PostAsync("/Controlo/Approve?handler=NaoAprovar",
            HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString()), ("nota", " "), ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}&nota_em_falta=1", semNota.Headers.Location?.ToString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Null((await db.Controlos.AsNoTracking().SingleAsync()).NotApprovedByOperatorId);
        }

        var naoAprovado = await client.PostAsync("/Controlo/Approve?handler=NaoAprovar",
            HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString()), ("nota", "Repetir a leitura 1."), ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", naoAprovado.Headers.Location?.ToString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal("4790", controlo.NotApprovedByOperatorId);
            Assert.Equal("Repetir a leitura 1.", controlo.ReviewNote);
            Assert.Null(controlo.ApprovedByOperatorId);
        }

        var pagina = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains("Não aprovado por Chefe Aprovação", pagina);
        Assert.Contains("Repetir a leitura 1.", pagina);

        var aprovado = await client.PostAsync("/Controlo/Approve?handler=Approve",
            HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", aprovado.Headers.Location?.ToString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal("4790", controlo.ApprovedByOperatorId);
            Assert.Null(controlo.NotApprovedByOperatorId);
            Assert.Null(controlo.ReviewNote);
        }
    }

    // E8 (atualizar só o que muda): aprovar e não aprovar gravam pelo
    // componente (sem data-dmo-nativo), com o dia e os filtros no envio e
    // no redirect, para a página voltar igual fora das zonas. Um segundo
    // envio igual não cria nada: o controlo é um só e a decisão é a última.
    [Fact]
    public async Task Revisao_Decisao_GravaSemRecarregar_ComContextoNoRedirect_SemRepetir()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        DateTime registadoEm;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.SingleAsync();
            var peso = new Peso { ControloId = controlo.Id, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            registadoEm = Assert.IsType<DateTime>(peso.RegistadoEmUtc);
        }

        var local = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(registadoEm);
        var dia = $"ano={local.Year}&mes={local.Month}&dia={local.Day}";

        // Sem sessão, nada se grava.
        using (var anonymous = CreateClient())
        {
            var anon = await anonymous.PostAsync("/Controlo/Approve?handler=Approve",
                HtmlFormHelpers.BuildFormPost(("jobon_id", jobOnId.ToString())));
            Assert.NotEqual(HttpStatusCode.OK, anon.StatusCode);
            Assert.DoesNotContain("/Controlo/Approve?jobon_id", anon.Headers.Location?.ToString() ?? "");
        }

        SeedChefe("4792", "provider-revisao-e8", "RevisaoE8Pass1!");
        using var client = await LoginClientAsync("4792", "RevisaoE8Pass1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}&{dia}&estado=pendente&procura=5447");

        // O formulário grava pelo componente e leva o contexto da lista.
        var form = Regex.Match(html, "<form method=\"post\"[^>]*id=\"revisaoDecisao\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.NotEmpty(form);
        Assert.Contains("data-dmo-parcial", form);
        Assert.DoesNotContain("data-dmo-nativo", form);
        Assert.Contains($"name=\"ano\" value=\"{local.Year}\"", form);
        Assert.Contains($"name=\"mes\" value=\"{local.Month}\"", form);
        Assert.Contains($"name=\"dia\" value=\"{local.Day}\"", form);
        Assert.Contains("name=\"estado\" value=\"pendente\"", form);
        Assert.Contains("name=\"procura\" value=\"5447\"", form);
        // Sem botões no topo: o Peso e os Pegamentos têm o seu Aprovar / Não
        // aprovar no canto, com id (o foco fica neles), e o «Consultar» abre
        // cada resumo (Diogo, 19/10/2026).
        Assert.DoesNotContain(">Consultar Peso</a>", html);
        foreach (var tipo in new[] { "peso", "pegamentos" })
            Assert.Matches($"href=\"/Controlo/Approve/Detalhe\\?controlo_id=\\d+&(amp;)?tipo={tipo}\"", html);
        Assert.DoesNotContain("tipo=ferramentas", html);
        Assert.DoesNotContain("revisaoAprovar-ferramentas", html);
        Assert.DoesNotContain("revisaoAprovar-pegamentos", html);
        foreach (var parte in new[] { "peso" })
        {
            Assert.Matches($"<button type=\"submit\" form=\"revisaoDecisao\" name=\"parte\" value=\"{parte}\" id=\"revisaoNaoAprovar-{parte}\"[^>]*>Não aprovar</button>\\s*"
                + $"<button type=\"submit\" form=\"revisaoDecisao\" name=\"parte\" value=\"{parte}\" id=\"revisaoAprovar-{parte}\"[^>]*>Aprovar</button>", html);
        }
        Assert.DoesNotContain("class=\"resumo-actions\"", html);
        // As zonas que a decisão muda existem sempre.
        foreach (var zona in new[] { "revisaoDetalhe", "revisaoResultados", "dmoCalendario", "revisaoSemData" })
            Assert.Matches($"id=\"{zona}\" data-dmo-regiao", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        (string, string)[] Corpo(params (string, string)[] mais) =>
            [("jobon_id", jobOnId.ToString()), ("ano", local.Year.ToString()), ("mes", local.Month.ToString()),
             ("dia", local.Day.ToString()), ("procura", "5447"), ("estado", "pendente"), ("__RequestVerificationToken", token), .. mais];
        var volta = $"/Controlo/Approve?jobon_id={jobOnId}&{dia}&procura=5447&estado=pendente";

        // A recusa (sem nota) volta com o contexto e a marca.
        var semNota = await client.PostAsync("/Controlo/Approve?handler=NaoAprovar", HtmlFormHelpers.BuildFormPost(Corpo(("nota", " "))));
        Assert.Equal(volta + "&nota_em_falta=1", semNota.Headers.Location?.ToString());

        var naoAprovado = await client.PostAsync("/Controlo/Approve?handler=NaoAprovar", HtmlFormHelpers.BuildFormPost(Corpo(("nota", "Rever"))));
        Assert.Equal(volta, naoAprovado.Headers.Location?.ToString());

        // Dois envios de «Aprovar» (o componente já ignora o segundo; o
        // servidor também não repete nada): um controlo, aprovado.
        for (var vez = 0; vez < 2; vez++)
        {
            var aprovado = await client.PostAsync("/Controlo/Approve?handler=Approve", HtmlFormHelpers.BuildFormPost(Corpo()));
            Assert.Equal(volta, aprovado.Headers.Location?.ToString());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal("4792", controlo.ApprovedByOperatorId);
            Assert.Null(controlo.NotApprovedByOperatorId);
            Assert.Null(controlo.ReviewNote);
        }

        // Com o filtro «Pendente», o controlo aprovado sai da lista e fica aberto ao lado.
        var depois = await GetPageHtmlAsync(client, volta);
        Assert.DoesNotContain("class=\"revisao-item", depois);
        Assert.Contains("revisao-pill big aprovado", depois);
        Assert.Contains(">Reaprovar</button>", depois);
        Assert.Contains("Aprovado por Chefe Aprovação", depois);

        // Um filtro desconhecido não volta no redirect; os registos sem data, sim.
        var semData = await client.PostAsync("/Controlo/Approve?handler=Approve", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("sem_data", "1"), ("estado", "outro"), ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}&sem_data=1", semData.Headers.Location?.ToString());
    }
}

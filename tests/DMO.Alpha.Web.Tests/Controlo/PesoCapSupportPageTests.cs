using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// Prova o apoio TP/calote no surface Peso (o «TP / calote · Apoio
/// separado» do dmo-html): o painel de factos mostra os volumes (marisa/BQ
/// e punção/CM) e a densidade do vidro com o processo da CM visível; o botão
/// abre o cálculo com a CALOTE EFETIVA (a do Job On quando existe, senão a
/// h_ref da Tool) calculado PELO BACKEND; a simulação recarrega pelo GET
/// nativo; a Tool sem TP mostra «Referência por completar» e o Peso segue
/// 100% intacto — o apoio nunca bloqueia (nunca gate). O preview tem
/// ENDPOINT do backend (?handler=CapSupport) reutilizando o mesmo cálculo
/// canónico do Core.
/// </summary>
public class PesoCapSupportPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoCapSupportPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    /// <summary>
    /// A base InMemory é PARTILHADA por todas as classes de teste do host:
    /// cada teste limpa o estado de domínio que tocou ao sair, pela ordem
    /// dependente→principal (a FK Controlo → JobOn é Restrict).
    /// </summary>
    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
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

        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
            };
            db.Templates.Add(template);
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

    /// <summary>
    /// O worked example do Owner: a produção 202601 da referência 1075C142
    /// na B1, com a CM 5447 (lote 3) com a geometria TP de referência
    /// (D 50 · R desenho 24,11 · h_ref 10 — a folha), a BQ T194 e a
    /// densidade NNPB; a calote do Job On é opcional (o parámetro). O Peso
    /// da produção já tem uma leitura real (199,64 g de água a 20 °C).
    /// </summary>
    private async Task<int> SeedProducaoComPesoAsync(decimal? caloteMm = 9m, bool comTp = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var machine = new Machine { Code = "B1" };
        var cmTool = new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        var bqTool = new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" };
        var mfTool = new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" };
        db.Machines.Add(machine);
        db.Tools.Add(cmTool);
        db.Tools.Add(bqTool);
        db.Tools.Add(mfTool);

        var cmTechnical = new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            VolumePuncaoCm3 = 30m
        };
        if (comTp)
        {
            cmTechnical.TpDiametroMm = 50m;
            cmTechnical.TpRaioMm = 24.11m;
            cmTechnical.TpSagittaMm = 10m;
        }

        db.ToolTechnicalContexts.Add(cmTechnical);
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 10m
        });
        db.GlassDensities.Add(new GlassDensity
        {
            Process = CmProcess.Nnpb,
            DensityGramsPerCm3 = 2.50m
        });

        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = machine,
            CaloteMm = caloteMm
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-9" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
        await db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();

        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id
        };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        db.PesoMeasurements.Add(new PesoMeasurement
        {
            PesoId = peso.Id,
            RowPosition = 1,
            CmSubject = "12",
            WaterWeightGrams = 199.64m
        });
        await db.SaveChangesAsync();

        return controlo.Id;
    }

    [Fact]
    public async Task ProductionMode_PainelDeFactos_MostraVolumesDensidadeEApoioComACaloteDoJobOn()
    {
        // O painel de factos (o renderFacts do dmo-html) com a calote
        // efetiva DO JOB ON (9 mm): R calculado (625+81)/18 = 39,222…;
        // ΔR = +2,972…; ΔV = 9,2174… − 10,34107… = −1,124…; drawing_delta
        // = 36,25 − 24,11 = +12,140 — tudo calculado PELO BACKEND.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4231", "Peso Operador", "provider-cap-1", "PesoCap1!");
        using var client = await LoginClientAsync("4231", "PesoCap1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        // Os três factos do painel com o processo da CM visível.
        Assert.Contains("Volume Marisa / BQ (cm³)", html);
        Assert.Contains("Volume Punção / CM (cm³)", html);
        Assert.Contains("Densidade do vidro (g/cm³)", html);
        Assert.Contains("<strong>2.5</strong>", html);
        Assert.Contains(">NNPB<", html);

        // O botão do apoio com o estado completo (com referência).
        Assert.Contains("TP / calote · Apoio separado", html);
        Assert.DoesNotContain("Referência por completar", html);
        Assert.Contains("9.22 cm³", html);           // o volume da calote efetiva (h = 9)
        Assert.Contains("D 50 · h 9 · Job On", html);

        // O corpo do apoio: as métricas da calote efetiva vs a referência
        // (o sinal «+» renderiza como a entidade &#x2B; — o precedente da
        // casa nos PegamentosPageTests).
        Assert.Contains("Origem do h: Job On", html);
        Assert.DoesNotContain("&#x2B;12.140 mm", html); // sem diferença ao desenho: o raio deixou de ser pedido
        Assert.DoesNotContain("R do desenho", html);
        Assert.Contains("Volume nominal</span><strong>10.34 cm³", html); // h nominal 10 (Diogo, 26/10/2026: volume nominal, do Job On e o Δ)
        Assert.DoesNotContain("R calculado", html);
        Assert.Contains("-1.12 cm³", html);           // ΔV = actual.V − reference.V (sinal preservado)

        // O facto humano é a referência da CM; o tool_id nunca é facto da folha.
        Assert.Contains("5447", html);
        Assert.DoesNotContain(">T3<", html);
    }

    [Fact]
    public async Task ProductionMode_SemCaloteNoJobOn_UsaAReferenciaDaTool()
    {
        // Job On sem calote (null por defeito da migração): a calote efetiva
        // cai na h_ref da Tool («Tool CM») e as diferenças são zero.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: null);
        SeedUser("4232", "Peso Operador", "provider-cap-2", "PesoCap2!");
        using var client = await LoginClientAsync("4232", "PesoCap2!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        Assert.Contains("Origem do h: Tool CM", html);
        Assert.Contains("D 50 · h 10 · Tool CM", html);
        Assert.Contains("10.34 cm³", html);
        // O sinal «+» renderiza como a entidade &#x2B; (precedente da casa).
        // Sem Job On não há h nem volume do Job On: só o nominal.
        Assert.Contains("<strong>—</strong>", html);
    }

    [Fact]
    public async Task ProductionMode_TampaoSoNaFolhaDoJobOn_LeODaFolha()
    {
        // Um Job On importado tem o Tampão só na folha (Diogo, 28/10/2026,
        // cmsg_01K75FTs9bQnbhhH7qttpgra7BVGbdHJFKtQowMdq1CL6g: «diz para
        // completar … se for ao Job On já lá está»): o «Diâmetro» e a
        // «Bacia PS» da folha valem quando o Job On não os tem gravados.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: null, comTp: false);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOnId = db.Controlos.Single(c => c.Id == controloId).JobOnId;
            db.JobOnFolhas.Add(new JobOnFolha { JobOnId = jobOnId, TpDiametroPs = "26,05", TpBaciaPs = "3" });
            await db.SaveChangesAsync();
        }
        SeedUser("4249", "Peso Operador", "provider-cap-folha", "PesoCapF1!");
        using var client = await LoginClientAsync("4249", "PesoCapF1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        Assert.DoesNotContain("Referência por completar", html);
        Assert.Contains("D 26.05 · h 3 · Job On", html);

        var cap = await client.GetAsync($"{PagePath}?handler=CapSupport&controlo_id={controloId}");
        using var json = JsonDocument.Parse(await cap.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("temReferencia").GetBoolean());
    }

    [Fact]
    public async Task ProductionMode_SemTP_ReferenciaPorCompletarEPesoIntacto()
    {
        // A CM sem TP: o apoio mostra «Referência por completar» e a
        // mensagem honesta — e o Peso funciona 100% intacto (o apoio nunca
        // bloqueia: as leituras e o cálculo principal continuam na folha).
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m, comTp: false);
        SeedUser("4233", "Peso Operador", "provider-cap-3", "PesoCap3!");
        using var client = await LoginClientAsync("4233", "PesoCap3!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        Assert.Contains("Referência por completar", html);
        Assert.Contains("Não bloqueia o Peso", html);
        Assert.Contains("O Peso pode continuar sem este apoio.", html);
        Assert.DoesNotContain("Completar na ficha da CM", html);
        Assert.DoesNotContain("R do desenho", html);

        // O Peso INTACTO: as leituras e o cálculo principal continuam —
        // 199,64 g a 20 °C → (200,2066 + 10 − 30) × 2,50 = 450,52 g.
        Assert.Contains("Leituras por CM", html);
        Assert.Contains(">450.52<", html);
    }

    [Fact]
    public async Task SimulacaoPorGet_RecalculaPeloBackendEAbreOApoio()
    {
        // A calote simulada (cap_h) chega pelo GET nativo: o apoio abre
        // expandido com a origem «Simulação» e o cálculo recalculado PELO
        // BACKEND — nunca matemática no browser, nunca gravação.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4234", "Peso Operador", "provider-cap-4", "PesoCap4!");
        using var client = await LoginClientAsync("4234", "PesoCap4!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&cap_h=8");

        Assert.Contains("<details class=\"tp-support\"", html);
        Assert.Contains("open=\"open\"", html);
        Assert.Contains("Origem do h: Simulação", html);
        Assert.Contains("h simulado", html);
        Assert.Contains("<strong>8 mm</strong>", html);
        Assert.Contains("-2.22 cm³", html);            // Δ volume: 8.12 − 10.34
        Assert.Contains("value=\"8\"", html);          // o campo veste o valor simulado

        // O facto do Job On NUNCA é alterado pela simulação.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(9m, jobOn.CaloteMm);
    }

    [Fact]
    public async Task SimulacaoIlegivel_AvisoHonestoSemBloqueio()
    {
        // «abc» no campo da simulação: aviso honesto, o apoio segue com o
        // h efetivo — nada bloqueia, nada é inventado.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4235", "Peso Operador", "provider-cap-5", "PesoCap5!");
        using var client = await LoginClientAsync("4235", "PesoCap5!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&cap_h=abc");

        Assert.Contains("não é utilizável", html);
        Assert.Contains("Origem do h: Job On", html);
        Assert.Contains("-1.12 cm³", html);
    }

    [Fact]
    public async Task CapSupportEndpoint_DevolveOCalculoDoBackend()
    {
        // O preview é ENDPOINT DO BACKEND (fonte única de verdade): o JSON
        // reutiliza o MESMO cálculo canónico do Core (PesoCapCalculation)
        // pela mesma query do render — D 50 / h 8 → R = 43,0625;
        // ΔR = 43,0625 − 36,25 = 6,8125.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4236", "Peso Operador", "provider-cap-6", "PesoCap6!");
        using var client = await LoginClientAsync("4236", "PesoCap6!");

        var response = await client.GetAsync(
            $"{PagePath}?handler=CapSupport&controlo_id={controloId}&calote=8");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.GetProperty("temReferencia").GetBoolean());
        Assert.Equal("Simulação", root.GetProperty("origemCalote").GetString());
        Assert.Equal(50m, root.GetProperty("diametroMm").GetDecimal());
        Assert.False(root.TryGetProperty("raioDesenhoMm", out _));
        Assert.Equal(10m, root.GetProperty("alturaReferenciaMm").GetDecimal());
        Assert.Equal(36.25m, root.GetProperty("raioReferenciaMm").GetDecimal());
        Assert.Equal(8m, root.GetProperty("alturaEfetivaMm").GetDecimal());
        Assert.Equal(43.0625m, root.GetProperty("raioCalculadoMm").GetDecimal());
        Assert.Equal(6.8125m, root.GetProperty("deltaRaioMm").GetDecimal());
        Assert.False(root.TryGetProperty("diferencaDesenhoMm", out _));
        Assert.InRange(root.GetProperty("volumeEfetivoCm3").GetDecimal(), 8.1220m, 8.1222m);
        Assert.InRange(root.GetProperty("deltaVolumeCm3").GetDecimal(), -2.220m, -2.218m);
    }

    [Fact]
    public async Task CapSupportEndpoint_SemCalote_UsaACaloteDoJobOn()
    {
        // Sem calote no pedido: o endpoint devolve a calote efetiva do
        // JOB ON (a mesma fonte do render do servidor).
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4237", "Peso Operador", "provider-cap-7", "PesoCap7!");
        using var client = await LoginClientAsync("4237", "PesoCap7!");

        var response = await client.GetAsync(
            $"{PagePath}?handler=CapSupport&controlo_id={controloId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.GetProperty("temReferencia").GetBoolean());
        Assert.Equal("Job On", root.GetProperty("origemCalote").GetString());
        Assert.Equal(9m, root.GetProperty("alturaEfetivaMm").GetDecimal());
    }

    [Fact]
    public async Task CapSupportEndpoint_SemTP_TemReferenciaFalse()
    {
        // Sem TP o endpoint devolve o estado «Referência por completar»
        // (temReferencia=false) — nunca um erro, nunca um bloqueio.
        var controloId = await SeedProducaoComPesoAsync(caloteMm: 9m, comTp: false);
        SeedUser("4238", "Peso Operador", "provider-cap-8", "PesoCap8!");
        using var client = await LoginClientAsync("4238", "PesoCap8!");

        var response = await client.GetAsync(
            $"{PagePath}?handler=CapSupport&controlo_id={controloId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.False(root.GetProperty("temReferencia").GetBoolean());
        Assert.Contains("O Peso pode continuar sem este apoio",
            root.GetProperty("estadoMensagem").GetString()!);
    }

    [Fact]
    public async Task ToolScopedMode_ApoioSemProducao_UsaAReferenciaDaTool()
    {
        // A pesagem antes da produção (tool-scoped): sem Job On, a calote
        // efetiva é a h_ref da Tool e o painel mostra os factos com o
        // processo da CM visível.
        await SeedProducaoComPesoAsync(caloteMm: 9m);
        SeedUser("4239", "Peso Operador", "provider-cap-9", "PesoCap9!");
        using var client = await LoginClientAsync("4239", "PesoCap9!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?cm_tool_id=5447&bq_tool_id=T194");

        Assert.Contains("TP / calote · Apoio separado", html);
        Assert.Contains("Origem do h: Tool CM", html);
        Assert.Contains("D 50 · h 10 · Tool CM", html);
        Assert.Contains("Volume Marisa / BQ (cm³)", html);
        Assert.Contains("Volume Punção / CM (cm³)", html);
        Assert.Contains("Densidade do vidro (g/cm³)", html);
        Assert.Contains(">NNPB<", html);
    }
}

using System.Net;
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
/// O «Comparar peso» (Diogo, 27/10/2026): no Histórico e na página de cada
/// Peso, um botão abre no painel lateral as CM compatíveis (a mesma
/// referência e a mesma máquina) e deixa trocar o tampão para ver quanto muda
/// o peso. Com dois Pesos marcados compara as leituras. Nada é gravado.
/// </summary>
public class PesoCompararPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/PesoComparar";

    private readonly CustomWebApplicationFactory _factory;

    public PesoCompararPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose() => ResetDomainState();

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string providerUserId, string password)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-comparar");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-teste-comparar",
                Name = "Comparar de teste",
                Capabilities = new[] { DmoCapabilities.ControloCreate }
            };
            db.Templates.Add(template);
        }

        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Comparar", ProviderUserId = providerUserId };
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync()
    {
        var id = Random.Shared.Next(5000, 9000).ToString();
        SeedUser(id, $"provider-comparar-{id}", "Comparar1!");
        var client = CreateClient();
        var login = await client.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await login.Content.ReadAsStringAsync())!;
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", id), ("password", "Comparar1!"), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> HtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Três CM ST100 na M21 (a 5447 duas vezes), uma na M23 e uma ST200 na
    /// M21; todas pesadas com o tampão 24.95 x 4.5. Devolve os ids pela ordem.
    /// </summary>
    private async Task<Dictionary<string, int>> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var m21 = new Machine { Code = "M21" };
        var m23 = new Machine { Code = "M23" };
        db.Machines.AddRange(m21, m23);
        db.Tools.AddRange(
            new Tool { ToolId = "CM-5447", Type = ToolType.Cm, Reference = "ST100", Lot = "5447" },
            new Tool { ToolId = "CM-5448", Type = ToolType.Cm, Reference = "ST100", Lot = "5448" },
            new Tool { ToolId = "CM-5512", Type = ToolType.Cm, Reference = "ST100", Lot = "5512" },
            new Tool { ToolId = "CM-9000", Type = ToolType.Cm, Reference = "ST200", Lot = "9000" },
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await db.SaveChangesAsync();

        var ids = new Dictionary<string, int>();
        var dia = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        async Task PesoAsync(string nome, string cm, Machine machine, string producao, int dias, decimal[] aguas)
        {
            var jobOn = new JobOn { Reference = "1075C142", ProductionNumber = producao, Machine = machine };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var peso = new Peso
            {
                CmToolId = cm,
                BqToolId = "BQ-1",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.5m,
                TampaoDiametroMm = 24.95m,
                TampaoCaloteMm = 4.5m,
                ControloId = controlo.Id,
                RegistadoEmUtc = dia.AddDays(dias)
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            for (var i = 0; i < aguas.Length; i++)
            {
                db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = i + 1, CmSubject = (i + 1).ToString(), WaterWeightGrams = aguas[i] });
            }

            await db.SaveChangesAsync();
            ids[nome] = peso.Id;
        }

        await PesoAsync("antigo", "CM-5447", m21, "202601", 0, [160m, 161m]);
        await PesoAsync("este", "CM-5447", m21, "202604", 3, [164m, 165m]);
        await PesoAsync("outraCm", "CM-5448", m21, "202605", 2, [163m, 166m]);
        await PesoAsync("outraMaquina", "CM-5512", m23, "202610", 4, [162m]);
        await PesoAsync("outraReferencia", "CM-9000", m21, "202611", 5, [150m]);
        return ids;
    }

    private (int Pesos, int Leituras) Contagem()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return (db.Pesos.Count(), db.PesoMeasurements.Count());
    }

    [Fact]
    public async Task DoPeso_MostraAsCmCompativeis_MesmaReferenciaEMaquina()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}");

        Assert.Contains("Comparar peso · CM ST100 · M21", html);
        Assert.Contains("CM ST100 · 5447</a> · este", html);
        Assert.Contains("CM ST100 · 5448", html);
        Assert.DoesNotContain("5512", html.Replace("value=\"M23\"", ""));
        Assert.DoesNotContain("ST200", html);
        Assert.DoesNotContain("202601", html);
        Assert.Contains("24.95 x 4.5", html);
        Assert.Contains("simulação, nada é gravado", html);
        Assert.Contains($"name=\"tp_{ids["este"]}\"", html);
        // Sem tampão simulado não há peso simulado.
        Assert.DoesNotContain("Média com os TP simulados", html);
    }

    [Fact]
    public async Task TampaoParaTodas_MudaOPesoPeloVolumeDaCalote_ENaoGrava()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();
        var antes = Contagem();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&tp={Uri.EscapeDataString("24.95 x 5.2")}");

        // ΔV = 1.345 − 1.148 = 0.197 cm³ × 2.5 g/cm³ = +0.49 g em cada CM e na média.
        Assert.Equal(3, CountOf(html, "+0.49 g"));
        Assert.Contains("Média com os TP simulados", html);
        Assert.Equal(antes, Contagem());
    }

    [Fact]
    public async Task TampaoDeUmaCm_SoMudaEssa_EUmSoNumeroMudaACalote()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&escolha=1&p={ids["este"]}&p={ids["outraCm"]}&tp_{ids["outraCm"]}=5.2");

        Assert.Equal(1, CountOf(html, "+0.49 g"));
        Assert.Contains("value=\"5.2\"", html);
    }

    [Fact]
    public async Task TampaoQueNaoSeLe_Avisa()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&tp_{ids["este"]}=abc");

        Assert.Contains("Escreva D x calote", html);
        Assert.Contains("aria-invalid=\"true\"", html);
    }

    [Fact]
    public async Task DoisMarcados_CompararLeituras_CmACm()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var aberta = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}");
        Assert.Contains("name=\"leituras\" value=\"1\"", aberta);

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&escolha=1&p={ids["este"]}&p={ids["outraCm"]}&leituras=1");

        Assert.Contains("data-dmo-list=\"Comparar leituras\"", html);
        Assert.Contains("Diferença média dos pares", html);

        var umSo = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&escolha=1&p={ids["este"]}&leituras=1");
        Assert.Contains("Marque dois Pesos para comparar as leituras.", umSo);
        Assert.DoesNotContain("name=\"leituras\" value=\"1\"", umSo);
    }

    [Fact]
    public async Task DoHistorico_PelaReferenciaDaCm_EOutraMaquina()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?cm=st1&maquina=M23");

        Assert.Contains("Comparar peso · CM ST100 · M23", html);
        Assert.Contains("CM ST100 · 5512", html);
        Assert.DoesNotContain("CM ST100 · 5447", html);
        Assert.Contains("id=\"pesoCompararCm\"", html);
        Assert.Contains("/Controlo/Peso?historico=1&amp;referencia=st1", html);
        Assert.DoesNotContain($"value=\"{ids["este"]}\" checked", html);
    }

    [Fact]
    public async Task OPesoEOHistorico_TemOBotao_EOPainelLateralAbreAPagina()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var peso = await HtmlAsync(client, $"/Controlo/Peso?peso_id={ids["este"]}");
        Assert.Contains($"href=\"/Controlo/PesoComparar?peso_id={ids["este"]}\">Comparar peso</a>", peso);

        var historico = await HtmlAsync(client, "/Controlo/Peso?historico=1&referencia=ST100");
        Assert.Contains("href=\"/Controlo/PesoComparar\" data-peso-comparar-cm=\"historicoReferencia\">Comparar peso</a>", historico);

        var suporte = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "wwwroot", "js", "dmo-support.js"));
        Assert.Contains("['/Controlo/PesoComparar', 'Comparar peso']", suporte);
    }

    private static int CountOf(string html, string value)
    {
        var text = WebUtility.HtmlDecode(html);
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}

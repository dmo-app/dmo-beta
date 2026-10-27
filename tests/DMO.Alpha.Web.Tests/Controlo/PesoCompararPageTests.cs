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
/// Peso, um botão abre no painel lateral os Pesos compatíveis (a mesma
/// referência da CM e a mesma máquina); um clique na linha escolhe com qual
/// comparar e aparece a tabela igual à do Peso. Em cada lado pode-se mudar só
/// a calote do tampão. Nada é gravado.
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
    public async Task DoPeso_ListaOsCompativeis_EComparaComOSeguinte()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}");

        Assert.Contains("Comparar peso · CM ST100 · M21", html);
        Assert.Contains("simulação, nada é gravado", html);
        Assert.Contains("data-dmo-list=\"Pesos compatíveis\"", html);
        Assert.Contains("CM ST100 · 5448", html);
        Assert.DoesNotContain("5512", html.Replace("value=\"M23\"", ""));
        Assert.DoesNotContain("ST200", html);
        Assert.DoesNotContain("202601", html);
        Assert.Contains("24.95 x 4.5", html);
        // Escolhe-se clicando na linha, que fica escolhida; nunca caixas de seleção.
        Assert.DoesNotContain("type=\"checkbox\"", html);
        Assert.Contains($"data-id=\"{ids["outraCm"]}\" data-dmo-open=\"/Controlo/PesoComparar?peso_id={ids["este"]}&amp;maquina=M21&amp;com={ids["outraCm"]}\" class=\"selected\" aria-selected=\"true\"", html);
        // A tabela igual à do Peso, CM com CM.
        Assert.Contains("data-peso-comparar-tabela", html);
        Assert.Contains("CM anterior · 202605", html);
        Assert.Equal(2, CountOf(html, "<td>1</td>"));
        Assert.Contains("name=\"calote_atual\"", html);
        Assert.Contains("name=\"calote_com\"", html);
        Assert.DoesNotContain("data-peso-comparar-simulada", html);
    }

    [Fact]
    public async Task CaloteSimulada_MudaSoEsseLadoPelaCaloteTampo_ENaoGrava()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();
        var antes = Contagem();

        var sem = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&com={ids["outraCm"]}");
        var com = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&com={ids["outraCm"]}&calote_atual=5.2");

        // ΔV = 1.345 − 1.148 = 0.197 cm³ × 2.5 g/cm³ = +0.49 g em cada CM do atual.
        Assert.Contains("calote 4.5 → 5.2 mm", com);
        Assert.Contains("(+0.49 g)", WebUtility.HtmlDecode(com));
        Assert.Equal(1, CountOf(com, "data-peso-comparar-simulada"));
        Assert.Contains("value=\"5.2\"", com);
        var difSem = DiferencasPeso(sem);
        var difCom = DiferencasPeso(com);
        Assert.Equal(3, difSem.Count);
        Assert.All(difSem.Zip(difCom), par => Assert.Equal(par.First + 0.49m, par.Second));
        Assert.Equal(antes, Contagem());

        // No outro lado, o Δ desce.
        var doOutro = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&com={ids["outraCm"]}&calote_com=5.2");
        Assert.All(difSem.Zip(DiferencasPeso(doOutro)), par => Assert.Equal(par.First - 0.49m, par.Second));
    }

    [Fact]
    public async Task CaloteQueNaoSeLe_Avisa()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&calote_atual=abc");

        Assert.Contains("Escreva a calote em mm, por exemplo 5.2.", html);
        Assert.Contains("aria-invalid=\"true\"", html);
        Assert.DoesNotContain("data-peso-comparar-simulada", html);
    }

    [Fact]
    public async Task OutraMaquina_ESemOutroPeso_DizQueNaoHaComQueComparar()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?peso_id={ids["este"]}&maquina=M23");

        // O Peso de onde se parte fica sempre; compara-se com a CM da outra máquina.
        Assert.Contains("CM ST100 · 5512", html);
        Assert.Contains("CM anterior · 202610", html);
        var so = await HtmlAsync(client, $"{PagePath}?peso_id={ids["outraReferencia"]}");
        Assert.Contains("Não há outro Peso desta CM nesta máquina para comparar.", so);
    }

    [Fact]
    public async Task DoHistorico_PelaReferenciaDaCm_EOutraMaquina()
    {
        var ids = await SeedAsync();
        using var client = await LoginAsync();

        var html = await HtmlAsync(client, $"{PagePath}?cm=st1&maquina=M21");

        Assert.Contains("Comparar peso · CM ST100 · M21", html);
        Assert.Contains("id=\"pesoCompararCm\"", html);
        Assert.Contains("id=\"pesoCompararAtual\"", html);
        Assert.Contains("/Controlo/Peso?historico=1&amp;referencia=st1", html);
        Assert.Contains("data-peso-comparar-tabela", html);

        var m23 = await HtmlAsync(client, $"{PagePath}?cm=ST100&maquina=M23");
        Assert.Contains("Comparar peso · CM ST100 · M23", m23);
        Assert.Contains("Não há outro Peso desta CM nesta máquina para comparar.", m23);
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

    /// <summary>O Δ peso de cada linha da tabela da comparação e da média, pela ordem.</summary>
    private static List<decimal> DiferencasPeso(string html)
    {
        var inicio = html.IndexOf("data-peso-comparar-tabela", StringComparison.Ordinal);
        var tabela = html[inicio..html.IndexOf("</table>", inicio, StringComparison.Ordinal)];
        return System.Text.RegularExpressions.Regex.Matches(WebUtility.HtmlDecode(tabela), @"<td class=""num""><strong>([+−-]?[0-9.]+)")
            .Select(m => decimal.Parse(m.Groups[1].Value.Replace("−", "-"), System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
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

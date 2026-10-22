using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Histórico (Diogo, 22/10/2026): a ficha tem um botão «Histórico» que abre
/// os Job On da ferramenta (as outras duas com o lote, nunca a própria) e o
/// menu da linha leva ao Controlo, à Reparação e à folha do Job On. Sem
/// ferramenta, o «Histórico das Ferramentas» abre vazio.
/// </summary>
public partial class FerramentasPagesTests
{
    private const string HistoricoPath = "/Ferramentas/Historico";

    private async Task<int> SeedJobOnDoCmAsync(string cmToolId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = db.Machines.Single(m => m.Code == "B1");
        db.Tools.AddRange(
            new Tool { ToolId = "Tmf0000001", Type = ToolType.Mf, Reference = "5447", Lot = "7" },
            new Tool { ToolId = "Tbq0000001", Type = ToolType.Bq, Reference = "T173", Lot = "02" });
        var jobOn = new JobOn { Reference = "5447", ProductionNumber = "202599", MachineId = machine.Id, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 9) };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = cmToolId });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = "Tmf0000001" });
        db.BqContexts.Add(new BqContext { JobOnId = jobOn.Id, ToolId = "Tbq0000001" });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task Ficha_TemOBotaoHistorico_QueAbreOsJobOnDaFerramenta()
    {
        SeedUser("4495", "Ferramentas Histórico", "provider-ferr-hist", "FerrHist95!");
        var toolId = await SeedRealToolAsync();
        var jobOnId = await SeedJobOnDoCmAsync(toolId);
        using var client = await LoginClientAsync("4495", "FerrHist95!");

        var ficha = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains($"id=\"ferrHistorico\" href=\"{HistoricoPath}?tool_id={toolId}\"", ficha);

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?tool_id={toolId}"));
        Assert.Contains("Produções — CM 5447 · Lote 3", html);
        Assert.Contains("5447 · 202599", html);
        Assert.Contains("01/03/2025 – 09/03/2025", html);
        Assert.Contains("MF 5447 · Lote 7", html);
        Assert.Contains("BQ T173 · Lote 02", html);
        // A própria ferramenta não é coluna; o menu da linha leva aos três sítios (jobon.create; sem Controlo nem RI não há esses).
        Assert.DoesNotContain("<th>CM</th>", html);
        Assert.Contains($"/JobOn?jobon_id={jobOnId}", html);
        Assert.Contains("Folha do Job On", html);
        Assert.DoesNotContain("<button", html.Split("id=\"histLista\"")[1].Split("</table>")[0]);
    }

    [Fact]
    public async Task HistoricoDasFerramentas_AbreVazio_EProcuraPelaReferencia()
    {
        SeedUser("4496", "Ferramentas Histórico", "provider-ferr-hist2", "FerrHist96!");
        var toolId = await SeedRealToolAsync();
        await SeedJobOnDoCmAsync(toolId);
        using var client = await LoginClientAsync("4496", "FerrHist96!");

        var vazio = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, HistoricoPath));
        Assert.Contains("Histórico das Ferramentas", vazio);
        Assert.Contains("id=\"histConvite\"", vazio);
        Assert.DoesNotContain("id=\"histLista\"", vazio);
        // A referência primeiro, depois os filtros, o «Limpar filtros» (sem «Procurar»).
        var ordem = new[] { "histReferencia", "histTipo", "histLinha", "histDe", "histAte", "histLimpar" }
            .Select(id => vazio.IndexOf($"id=\"{id}\"", StringComparison.Ordinal)).ToList();
        Assert.All(ordem, i => Assert.True(i > 0));
        Assert.Equal(ordem.Order(), ordem);
        Assert.Contains("href=\"/Ferramentas/Historico\"", vazio);

        var procura = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?referencia=5447&tipo=CM"));
        Assert.Contains("CM 5447 · Lote 3", procura);
        Assert.Contains("5447 · 202599", procura);
    }

    [Fact]
    public async Task Historico_SemAChaveDasFerramentas_NaoAbre()
    {
        SeedUser("4497", "Sem Template", "provider-ferr-hist3", "FerrHist97!", withOperationalAccess: false);
        using var client = await LoginClientAsync("4497", "FerrHist97!");

        var response = await client.GetAsync(HistoricoPath);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}

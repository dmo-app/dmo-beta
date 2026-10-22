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
            new Tool { ToolId = "Tbq0000001", Type = ToolType.Bq, Reference = "5447T173", Lot = "02" });
        var jobOn = new JobOn { Reference = "5447T173", ProductionNumber = "202599", MachineId = machine.Id, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 9) };
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
        // Colunas (Diogo, 22/10/2026): Referência · Produção · Linha · Data · Dias · CM · BQ · MF (cada uma com a sua % de uso), a da ferramenta vista destacada.
        var cabecalho = new[] { "<th>Referência</th>", "<th>Produção</th>", "<th>Linha</th>", "<th>Data</th>", "<th>Dias</th>",
                "<th class=\"hist-destaque\" data-hist-tipo=\"CM\">CM</th>", "<th data-hist-tipo=\"BQ\">BQ</th>", "<th data-hist-tipo=\"MF\">MF</th>" }
            .Select(th => html.IndexOf(th, StringComparison.Ordinal)).ToList();
        Assert.All(cabecalho, i => Assert.True(i > 0));
        Assert.Equal(cabecalho.Order(), cabecalho);
        Assert.Contains("<td>5447T173</td>", html);
        Assert.Contains(">202599</summary>", html);
        Assert.Contains("<td>B1</td>", html);
        // 01/03 a 09/03: 9 dias (o do início conta).
        Assert.Contains("<td>01/03/2025 – 09/03/2025</td>\n              <td>9</td>", html.Replace("\r\n", "\n"));
        Assert.DoesNotContain("5447T173 · 202599", html);
        Assert.Contains("01/03/2025 – 09/03/2025", html);
        Assert.Contains("MF 5447 · Lote 7", html);
        // A BQ só com a sua parte da referência (a total é MF + BQ).
        Assert.Contains("BQ T173 · Lote 2", html);
        Assert.DoesNotContain("BQ 5447T173", html);
        // O lote gravado «02» mostra-se sem o zero.
        Assert.DoesNotContain("Lote 02", html);
        // A própria ferramenta não é coluna; o menu da linha leva aos três sítios (jobon.create; sem Controlo nem RI não há esses).
        // As três ferramentas em todas as linhas; só a coluna da CM vista fica destacada.
        Assert.Contains("<td class=\"hist-destaque\">", html);
        Assert.Contains("MF 5447 · Lote 7", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<th class=\"hist-destaque\""));
        Assert.DoesNotContain("<th>Ferramenta</th>", html);
        Assert.DoesNotContain("Outras ferramentas", html);
        Assert.DoesNotContain("<th>% uso</th>", html);
        // A % de uso de cada uma (sem leituras, «—»).
        Assert.Contains("CM 5447 · Lote 3<span class=\"hist-uso\"> · —</span>", html);
        Assert.Contains("BQ T173 · Lote 2<span class=\"hist-uso\"> · —</span>", html);
        // Sem controlo.create (o template de teste tem jobon.create e ferramentas), o menu não tem o Controlo nem a Reparação.
        Assert.DoesNotContain(">Controlo</a>", html);
        Assert.DoesNotContain("/Controlo/Resumo?jobon_id=", html);
        Assert.DoesNotContain(">Reparação</a>", html);
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
        Assert.Contains(">202599</summary>", procura);
        // As mesmas colunas; o Tipo escolhido (CM) é o destacado, sem a coluna Ferramenta.
        Assert.Contains("<th class=\"hist-destaque\" data-hist-tipo=\"CM\">CM</th>", procura);
        Assert.DoesNotContain("<th>Ferramenta</th>", procura);
        Assert.Contains("BQ T173 · Lote 2", procura);
        Assert.DoesNotContain("BQ 5447T173", procura);

        // Com o Tipo BQ, a coluna BQ é a destacada e a BQ mostra só a sua parte.
        var bq = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?referencia=5447&tipo=BQ"));
        Assert.Contains("<th class=\"hist-destaque\" data-hist-tipo=\"BQ\">BQ</th>", bq);
        Assert.Contains("<th data-hist-tipo=\"CM\">CM</th>", bq);
        Assert.Matches(new System.Text.RegularExpressions.Regex("<td class=\"hist-destaque\">\\s*<span class=\"hist-ferr\">BQ T173 · Lote 2<"), bq);
        Assert.DoesNotContain("BQ 5447T173", bq);
    }

    [Fact]
    public async Task Historico_SemAChaveDasFerramentas_NaoAbre()
    {
        SeedUser("4497", "Sem Template", "provider-ferr-hist3", "FerrHist97!", withOperationalAccess: false);
        using var client = await LoginClientAsync("4497", "FerrHist97!");

        var response = await client.GetAsync(HistoricoPath);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Historico_ComControloERi_OMenuTemOsTresDestinos()
    {
        // Um utilizador tem um só template: a chefia com as quatro chaves.
        SeedUser("4498", "Chefia Histórico", "provider-ferr-hist4", "FerrHist98!", withOperationalAccess: false);
        var toolId = await SeedRealToolAsync();
        var jobOnId = await SeedJobOnDoCmAsync(toolId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = db.Users.Single(u => u.OperatorId == "4498");
            var chefia = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-hist-chefia",
                Name = "Chefia histórico",
                Capabilities = new[] { DMO.Alpha.Core.Admin.DmoCapabilities.Ferramentas, DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate, DMO.Alpha.Core.Admin.DmoCapabilities.ControloCreate, DMO.Alpha.Core.Admin.DmoCapabilities.ReparacaoInterna }
            };
            db.Templates.Add(chefia);
            db.SaveChanges();
            db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment { UserId = user.Id, TemplateId = chefia.Id });
            db.SaveChanges();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            // Leituras antes e depois da 202599 (01/03 a 09/03/2025) para a CM e o MF; a BQ sem leituras.
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            DateTime Dia(int mes, int dia) => new(2025, mes, dia, 11, 0, 0, DateTimeKind.Utc);
            db.FerramentaUtilizacoes.AddRange(
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 10m, RegistadaEmUtc = Dia(3, 1) },
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 18m, RegistadaEmUtc = Dia(3, 9) },
                new FerramentaUtilizacao { ToolId = "Tmf0000001", Percentagem = 30m, RegistadaEmUtc = Dia(2, 27) },
                new FerramentaUtilizacao { ToolId = "Tmf0000001", Percentagem = 35.5m, RegistadaEmUtc = Dia(3, 10) });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4498", "FerrHist98!");

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?tool_id={toolId}"));

        Assert.Contains($"href=\"/Controlo/Resumo?jobon_id={jobOnId}\">Controlo</a>", html);
        Assert.Contains($"href=\"/ReparacaoInterna/Consultar?referencia=5447T173&producao={jobOnId}\">Reparação</a>", html);
        Assert.Contains($"href=\"/JobOn?jobon_id={jobOnId}\">Folha do Job On</a>", html);
        // A % de uso das três ferramentas, cada uma na sua célula.
        Assert.Contains("CM 5447 · Lote 3<span class=\"hist-uso\"> · 8 %</span>", html);
        Assert.Contains("MF 5447 · Lote 7<span class=\"hist-uso\"> · 5.5 %</span>", html);
        Assert.Contains("BQ T173 · Lote 2<span class=\"hist-uso\"> · —</span>", html);
    }

    [Fact]
    public async Task HistoricoDasFerramentas_AcimaDoLimite_MostraOAviso()
    {
        SeedUser("4499", "Ferramentas Histórico", "provider-ferr-hist5", "FerrHist99!");
        await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.AddRange(Enumerable.Range(1, DMO.Alpha.Infrastructure.Tools.FerramentaHistoricoQuery.MaxFerramentas + 1)
                .Select(i => new Tool { ToolId = $"Tmuitas{i:000}", Type = ToolType.Cm, Reference = "9999", Lot = i.ToString() }));
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4499", "FerrHist99!");

        var acima = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?referencia=9999"));
        var dentro = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{HistoricoPath}?referencia=5447"));

        Assert.Contains("id=\"histCortada\"", acima);
        Assert.Contains("Há mais resultados", acima);
        Assert.DoesNotContain("id=\"histCortada\"", dentro);
    }
}

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
        // As quatro sempre, pela mesma ordem; sem acesso ficam apagadas (Diogo, 29/10/2026).
        Assert.Contains($"href=\"/JobOn?jobon_id={jobOnId}\">Job On</a>", html);
        Assert.Contains("aria-disabled=\"true\">Controlo</span>", html);
        Assert.DoesNotContain("<button", html.Split("id=\"histLista\"")[1].Split("</table>")[0]);
    }

    /// <summary>Sem separador «Histórico» (Diogo, 28/10/2026: «deixar apenas pesquisar»): sem ferramenta, vai para o Pesquisar.</summary>
    [Fact]
    public async Task HistoricoSemFerramenta_VaiParaOPesquisar()
    {
        SeedUser("4496", "Ferramentas Histórico", "provider-ferr-hist2", "FerrHist96!");
        using var client = await LoginClientAsync("4496", "FerrHist96!");

        var response = await client.GetAsync(HistoricoPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Ferramentas", response.Headers.Location?.OriginalString);
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
        Assert.Contains($"href=\"/JobOn?jobon_id={jobOnId}\">Job On</a>", html);
        // A % de uso das três ferramentas, cada uma na sua célula; um clique na % abre o histórico do uso (Diogo, 27/10/2026).
        Assert.Matches("CM 5447 · Lote 3<span class=\"hist-uso\"> · <a class=\"dmo-uso-link\" href=\"/Ferramentas/Utilizacao\\?tool_id=[^\"]+\"[^>]*>8 %</a></span>", html);
        Assert.Matches("MF 5447 · Lote 7<span class=\"hist-uso\"> · <a class=\"dmo-uso-link\" href=\"/Ferramentas/Utilizacao\\?tool_id=[^\"]+\"[^>]*>5.5 %</a></span>", html);
        Assert.Contains("BQ T173 · Lote 2<span class=\"hist-uso\"> · —</span>", html);
    }

    /// <summary>
    /// Os separadores das Ferramentas (Diogo, 22/10/2026): «Ver» passa a
    /// «Pesquisar» e «Reparação Programada» a «Registar Reparação»; os
    /// endereços ficam. Só a barra dos separadores do módulo conta.
    /// </summary>
    [Fact]
    public async Task BarraDasFerramentas_PesquisarERegistarReparacao_SemOsNomesAntigos()
    {
        SeedUser("4500", "Ferramentas Separadores", "provider-ferr-hist6", "FerrHist00!");
        using var client = await LoginClientAsync("4500", "FerrHist00!");

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, "/Ferramentas"));
        var barra = System.Text.RegularExpressions.Regex.Match(html,
            "<nav class=\"tabs dmo-secondary-nav\"[^>]*>.*?</nav>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.NotEmpty(barra);
        var links = System.Text.RegularExpressions.Regex.Matches(barra, "<a[^>]*href=\"([^\"]*)\"[^>]*>\\s*([^<]*?)\\s*</a>")
            .Select(m => (Texto: m.Groups[2].Value, Href: m.Groups[1].Value))
            .ToList();

        // 1. Os nomes novos, com os endereços de sempre.
        Assert.Contains(("Pesquisar", "/Ferramentas"), links);
        Assert.Contains(("Registar Reparação", "/Ferramentas/ReparacaoProgramada"), links);

        // 2. Só depois: os nomes antigos já não estão na barra.
        Assert.DoesNotContain("Reparação Programada", barra);
        Assert.DoesNotContain(links, l => l.Texto == "Ver");
        // Sem «Histórico» (Diogo, 28/10/2026: «deixar apenas pesquisar»).
        Assert.DoesNotContain(links, l => l.Texto == "Histórico");
    }
}

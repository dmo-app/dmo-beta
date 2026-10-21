using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O fabrico pelo SAP (Diogo, 21/10/2026): o relatório de fabrico lido da
/// pasta do SAP vai pelo mesmo caminho da utilização; na ficha da
/// ferramenta, um clique no % da Utilização abre a janela com os Job On em
/// que ela foi a CM, o MF ou a BQ, o que o SAP diz que produziram e o delta;
/// cada produção leva à ficha do Job On.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    private (string ToolId, string Referencia, string Maquina) SeedJobOnComCm()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var marca = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var maquina = new Machine { Code = "Z" + marca };
        var toolId = "T" + Guid.NewGuid().ToString("N")[..10];
        var referencia = "55" + marca;
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = referencia, Lot = "2" });
        var jobOn = new JobOn
        {
            Reference = referencia, ProductionNumber = "202501", Machine = maquina,
            InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4)
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = toolId });
        db.SaveChanges();
        return (toolId, referencia, maquina.Code);
    }

    private static string RelatorioDeFabrico(string material, string linha) =>
        "        6- Manufacturing Selected\n"
        + "\tMaterial\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tGross Prod\t Net Prod.\n"
        + $"\t{material}\t{linha}\tMG\t21.01.2025\t04.02.2025\t       14\t1.404.234\t1.309.264\n";

    [Fact]
    public async Task ORelatorioDeFabrico_MostraOQueCadaJobOnDaFerramentaProduziu()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        var client = await LoginOperatorAsync("7105", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync(FerramentasPath)).Content.ReadAsStringAsync())!;

        var resposta = await client.PostAsync($"{FerramentasPath}?handler=ImportarUsoSap", HtmlFormHelpers.BuildFormPost(
            ("conteudo", RelatorioDeFabrico(referencia + "BRA", maquina)),
            ("ficheiro", "fabrico.txt"),
            ("modificado", "1792562400000"),
            ("__RequestVerificationToken", token)));

        using (var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("gravado").GetBoolean());
            Assert.Equal("Fabrico lido do SAP: 1 produção nova.", json.RootElement.GetProperty("mensagem").GetString());
        }

        // O % da Utilização abre a janela das produções, que vem fechada com a ficha.
        var ficha = await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync();
        Assert.Matches("<button\\b[^>]*id=\"ferr-utilizacao-detalhes\"[^>]*data-dmo-dialog=\"ferrProducoes\"", ZonaUsoSap(ficha, "ferrUtilizacao"));
        Assert.Matches("<dialog id=\"ferrProducoes\" class=\"dmo-dialog ferr-producoes-janela\"[^>]*>", ficha);
        Assert.DoesNotContain("id=\"ferr-producoes-abrir\"", ficha, StringComparison.Ordinal);

        // A produção, o produzido (a bruta, pt-PT: espaço entre os milhares) e o caminho para a ficha do Job On.
        var producoes = WebUtility.HtmlDecode(ZonaUsoSap(ficha, "ferrProducoes"));
        Assert.DoesNotContain("data-dmo-dialog-aberta=\"1\"", producoes[..producoes.IndexOf('>')], StringComparison.Ordinal);
        Assert.Contains("202501", producoes, StringComparison.Ordinal);
        Assert.Contains($"title=\"{maquina} · 21/01/2025 a 04/02/2025\"", producoes, StringComparison.Ordinal);
        Assert.Contains(1_404_234.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")), producoes, StringComparison.Ordinal);
        Assert.Matches("<a class=\"ferr-armazem-link\" href=\"/JobOn\\?jobon_id=\\d+\"><strong>202501</strong></a>", producoes);
        Assert.DoesNotContain("id=\"ferrProducoesSemSap\"", producoes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ODetalhesDaUtilizacao_MostraODeltaDeCadaProducaoEOTotal()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.SapFabricos.Add(new SapFabrico
            {
                Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4),
                ProducaoBruta = 1_404_234, ProducaoLiquida = 1_309_264, LidoEmUtc = DateTime.UtcNow
            });
            // Antes do início 40 %, depois do fim 47,5 %.
            db.FerramentaUtilizacoes.AddRange(
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 40m, RegistadaEmUtc = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc) },
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 47.5m, RegistadaEmUtc = new DateTime(2025, 2, 6, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7108", DmoCapabilities.Ferramentas);

        // Com ?jobon=1 a janela abre com a página; o % fica sozinho no facto.
        var ficha = await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync();
        Assert.Contains(">47.5 %</button></div>", ZonaUsoSap(ficha, "ferrUtilizacao") + "</div>", StringComparison.Ordinal);
        Assert.Contains("data-dmo-dialog-aberta=\"1\"", Regex.Match(ficha, "<dialog id=\"ferrProducoes\"[^>]*>").Value, StringComparison.Ordinal);
        var quadro = WebUtility.HtmlDecode(ZonaUsoSap(ficha, "ferrProducoes"));

        Assert.Contains("Utilização por produção", quadro, StringComparison.Ordinal);
        Assert.Contains("+7.5 %", quadro, StringComparison.Ordinal);
        var total = WebUtility.HtmlDecode(ZonaUsoSap(quadro, "ferrProducoesTotal"));
        Assert.Contains("Total · 1 produção", total, StringComparison.Ordinal);
        Assert.Contains(1_404_234.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")), total, StringComparison.Ordinal);
        Assert.Contains("47.5 %", total, StringComparison.Ordinal);
        Assert.Contains("Hoje 47.5 %", WebUtility.HtmlDecode(Regex.Replace(ZonaUsoSap(quadro, "ferrProducoesHoje"), "<[^>]+>", "")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemORelatorioDeFabrico_AProducaoFicaComTraco()
    {
        var (toolId, _, _) = SeedJobOnComCm();
        var client = await LoginOperatorAsync("7106", DmoCapabilities.Ferramentas);

        var producoes = ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync(), "ferrProducoes");

        Assert.Contains("202501", producoes, StringComparison.Ordinal);
        Assert.Contains("o SAP ainda não deu o fabrico desse Job On", ZonaUsoSap(producoes, "ferrProducoesSemSap"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UmaFerramentaSemJobOn_DizQueNaoEsteveEmNenhum()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var toolId = SeedToolComNomeTecnico(nome, "5");
        var client = await LoginOperatorAsync("7107", DmoCapabilities.Ferramentas);

        var producoes = ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync(), "ferrProducoes");

        Assert.Contains("Esta ferramenta ainda não esteve em nenhum Job On.", producoes, StringComparison.Ordinal);
    }
}

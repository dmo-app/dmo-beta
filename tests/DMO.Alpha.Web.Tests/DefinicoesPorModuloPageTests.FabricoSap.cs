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
/// pasta do SAP vai pelo mesmo caminho da utilização; no Opções da ficha da
/// ferramenta, «Produções (Job On)» mostra os Job On em que ela foi a CM, o
/// MF ou a BQ e o que o SAP diz que produziram.
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

        // O link do Opções da ficha abre a tabela.
        var ficha = await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"ferrProducoes\"", ficha, StringComparison.Ordinal);
        var link = Regex.Match(ZonaUsoSap(ficha, "ferrOpcoes"), "<a\\b[^>]*id=\"ferr-producoes-abrir\"[^>]*>");
        Assert.True(link.Success);
        var href = WebUtility.HtmlDecode(Regex.Match(link.Value, "href=\"([^\"]+)\"").Groups[1].Value);
        var comProducoes = await (await client.GetAsync(href)).Content.ReadAsStringAsync();

        // Os números como a ficha os escreve (pt-PT: espaço entre os milhares).
        var producoes = WebUtility.HtmlDecode(ZonaUsoSap(comProducoes, "ferrProducoes"));
        Assert.Contains("202501", producoes, StringComparison.Ordinal);
        Assert.Contains("21/01/2025", producoes, StringComparison.Ordinal);
        Assert.Contains(maquina, producoes, StringComparison.Ordinal);
        Assert.Contains(1_404_234.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")), producoes, StringComparison.Ordinal);
        Assert.Contains(1_309_264.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")), producoes, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ferrProducoesSemSap\"", producoes, StringComparison.Ordinal);
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

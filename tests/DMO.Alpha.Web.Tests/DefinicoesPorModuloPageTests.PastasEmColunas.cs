using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As pastas de «Este computador» em duas colunas fixas (Diogo, 27/10/2026:
/// «ao associar pasta nas Definições e ligar, o UI teve um break momentâneo»):
/// com o column-count, ligar a pasta do SAP mudava a altura do cartão e os
/// outros saltavam de coluna. A primeira coluna leva metade dos cartões
/// (arredondada para cima), pela ordem de sempre.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task AsPastasDesteComputador_FicamCadaUmaNaSuaColuna()
    {
        var todas = await LoginOperatorAsync("7121", DmoCapabilities.Ferramentas, DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.Boquilhas);
        var html = await (await todas.GetAsync("/Definicoes?secao=computador")).Content.ReadAsStringAsync();

        var colunas = Colunas(html);
        Assert.Equal(2, colunas.Length);
        Assert.Equal(["ctlDefPastaPdf", "bqDefExcel"], Cartoes(colunas[0]));
        Assert.Equal(["jobonDefImagens", "ferrDefSap"], Cartoes(colunas[1]));

        // Só um cartão: uma coluna.
        var so = await LoginOperatorAsync("7122", DmoCapabilities.Ferramentas);
        var umaColuna = Colunas(await (await so.GetAsync("/Definicoes?secao=computador")).Content.ReadAsStringAsync());
        Assert.Equal(["ferrDefSap"], Cartoes(Assert.Single(umaColuna)));
    }

    private static string[] Colunas(string html)
    {
        var inicio = html.IndexOf("<div class=\"def-pastas\">", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Falta a grelha das pastas.");
        return html[inicio..].Split("<div class=\"def-pastas-coluna\">", StringSplitOptions.None).Skip(1).ToArray();
    }

    private static string[] Cartoes(string coluna) =>
        System.Text.RegularExpressions.Regex.Matches(coluna, "<section id=\"(ctlDefPastaPdf|bqDefExcel|jobonDefImagens|ferrDefSap)\"")
            .Select(m => m.Groups[1].Value).ToArray();
}

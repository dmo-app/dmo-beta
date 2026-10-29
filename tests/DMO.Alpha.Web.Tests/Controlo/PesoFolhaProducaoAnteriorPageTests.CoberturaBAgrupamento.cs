namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // DMO-491: a tabela da comparação agrupa as colunas em «Este controlo»,
    // o controlo anterior e a «Diferença», por esta ordem, e a seguir vêm as
    // colunas de cada grupo.
    [Fact]
    public async Task Folha_TabelaDaComparacao_AgrupaEsteControlo_ControloAnterior_EDiferenca_PorEstaOrdem()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4878", "Peso Operador", "provider-folha-grupos", "FolhaGrupos15!");
        using var client = await LoginClientAsync("4878", "FolhaGrupos15!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        var inicio = html.IndexOf("data-peso-comparacao-tabela", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a página não tem a tabela da comparação");
        var tabela = html[inicio..html.IndexOf("</table>", inicio, StringComparison.Ordinal)];

        Assert.Contains("<th colspan=\"3\" scope=\"colgroup\">Este controlo</th>", tabela, StringComparison.Ordinal);
        Assert.Contains("<th colspan=\"3\" scope=\"colgroup\">Controlo anterior · 202604</th>", tabela, StringComparison.Ordinal);
        Assert.Contains("<th colspan=\"2\" scope=\"colgroup\">Diferença</th>", tabela, StringComparison.Ordinal);
        var este = PosicaoNoHtml.Posicao(tabela, ">Este controlo</th>");
        var anterior = PosicaoNoHtml.Posicao(tabela, ">Controlo anterior · 202604</th>");
        var diferenca = PosicaoNoHtml.Posicao(tabela, ">Diferença</th>");
        Assert.True(este < anterior && anterior < diferenca, "Este controlo, Controlo anterior, Diferença");
        // Os grupos vêm antes das colunas de cada um.
        Assert.True(diferenca < PosicaoNoHtml.Posicao(tabela, "<th>Volume (cm³)</th>"));
    }
}

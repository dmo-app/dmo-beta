namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // DMO-491, mudado por pedido do Diogo (29/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraM8r59Af85tn7f3zTFfP4DF
    // e cmsg_01K75FTs9bQnbhhH7qttpgraFUfGGfDxwiZwrLfL7HKTHW): a tabela da comparação
    // põe lado a lado, por grandeza, o atual, o anterior e a diferença (volume e peso),
    // e as colunas do controlo anterior levam um fundo cinza claro.
    [Fact]
    public async Task Folha_TabelaDaComparacao_AgrupaPorGrandeza_AtualAnteriorEDiferenca_ComAnteriorACinza()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4878", "Peso Operador", "provider-folha-grupos", "FolhaGrupos15!");
        using var client = await LoginClientAsync("4878", "FolhaGrupos15!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        var inicio = html.IndexOf("data-peso-comparacao-tabela", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a página não tem a tabela da comparação");
        var tabela = html[inicio..html.IndexOf("</table>", inicio, StringComparison.Ordinal)];

        Assert.Contains("<th colspan=\"3\" scope=\"colgroup\">Volume (cm³)</th>", tabela, StringComparison.Ordinal);
        Assert.Contains("<th colspan=\"3\" scope=\"colgroup\">Peso (g)</th>", tabela, StringComparison.Ordinal);
        var volume = PosicaoNoHtml.Posicao(tabela, ">Volume (cm³)</th>");
        var peso = PosicaoNoHtml.Posicao(tabela, ">Peso (g)</th>");
        Assert.True(volume < peso, "Volume e depois Peso");
        // Em cada grandeza: atual, anterior (cinza) e diferença.
        Assert.Matches("<th>Atual</th>\\s*<th class=\"peso-cmp-ant\">Anterior</th>\\s*<th>Diferença</th>\\s*<th>Atual</th>\\s*<th class=\"peso-cmp-ant\">Anterior</th>\\s*<th>Diferença</th>", tabela);
        Assert.Contains("class=\"peso-cmp-ant\"", tabela, StringComparison.Ordinal);
    }
}

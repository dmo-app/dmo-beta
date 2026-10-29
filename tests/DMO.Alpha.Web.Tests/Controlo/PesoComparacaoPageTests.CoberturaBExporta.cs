namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoComparacaoPageTests
{
    // Guarda o HTML da Comparação de Peso (DMO-469) para
    // tests/visual/cobertura_b_browser.py medir a cor da linha e os campos.
    [Fact]
    public async Task Comparacao_ComBase_TemALinhaDestacada_ParaOBrowser()
    {
        var (_, jobOnId, _) = await SeedProducaoAsync();
        SeedUser("4882", "Peso Operador", "provider-cmp-b-export", "PesoCmpB1!");
        using var client = await LoginClientAsync("4882", "PesoCmpB1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}");

        Assert.Contains("data-cmp-base", html);
        Assert.Contains("class=\"peso-linha-destaque\"", html);
        Assert.Contains("data-cmp-add-row", html);
        ExportarParaOBrowser.Gravar("cobertura-b-peso-comparacao.html", html);
    }
}

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // DMO-479 e DMO-493: a ficha da ferramenta cabe em 1366×768 sem deslizar e
    // tem o «← Voltar» por cima do título. A página vai para o Chromium
    // (tests/visual/cobertura_a2_browser.py), que mede o encaixe no ecrã.
    [Fact]
    public async Task Ficha_ParaOBrowser_CabeNoEcraSemDeslizar()
    {
        SeedUser("4479", "Ferramentas Ficha", "provider-ferr-ficha-479", "FerrFicha79!");
        var toolId = await SeedRealToolAsync();
        using var client = await LoginClientAsync("4479", "FerrFicha79!");

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");

        Assert.Contains("id=\"ferrGeral\"", html);
        Assert.Contains("id=\"ferrVoltarTexto\"", html);
        ExportarParaOBrowser.Gravar("cobertura-a2-ferr-ficha.html", html);
    }
}

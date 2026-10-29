using System.Net;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    /// <summary>
    /// O Resumo do Controlo com o painel de apoio e o botão «Registar» dos Pegamentos,
    /// exportado para o Chromium (tests/visual/cobertura_a1_browser.py, DMO-485): lá
    /// mede-se a 1366×768 que o painel cabe no ecrã e que o botão fica à vista.
    /// </summary>
    [Fact]
    public async Task ResumoComPainelDeApoioEBotaoDosPegamentos_ExportadoParaOBrowser()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedOperacional("4725", "provider-oper-a1-aspeto", "OperAspeto1!");
        using var client = await LoginClientAsync("4725", "OperAspeto1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");

        Assert.Contains("id=\"dmoSupport\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Registar os Pegamentos\"", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        ExportarParaOBrowser.Gravar("cobertura-a1-controlo-resumo.html", html);
    }
}

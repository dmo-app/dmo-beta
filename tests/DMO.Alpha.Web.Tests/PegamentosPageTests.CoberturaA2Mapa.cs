using System.Net;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // DMO-563 (A-0295): nos Pegamentos o Mapa de limites volta a ficar por
    // baixo das leituras, e o Mapa já não tem texto de ajuda.
    [Fact]
    public async Task FolhaComMedicoes_OMapaDeLimitesFicaPorBaixoDasLeituras_SemTextoDeAjuda()
    {
        SeedUser("4563", "Pegamentos Mapa", "provider-peg-mapa", "PegMapa563!");
        using var client = await LoginClientAsync("4563", "PegMapa563!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var posted = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", contraCostura: "69.90", gargalo: "70.00");
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        var bruto = await GetPageHtmlAsync(client, url);
        var html = WebUtility.HtmlDecode(bruto);

        ExportarParaOBrowser.Gravar("cobertura-a2-pegamentos-mapa.html", bruto);

        // O mapa aparece (o «aparece» antes do «não aparece») e fica depois das leituras.
        Assert.True(Posicao(html, "data-peg-registo") >= 0);
        Assert.True(Posicao(html, "id=\"peg-map-title\">Mapa de limites") > html.LastIndexOf("data-peg-registo", StringComparison.Ordinal));
        // Sem a ajuda do mapa.
        Assert.DoesNotContain("peg-map-help", html);
        // A tolerância é só o valor, sem frases a explicá-la.
        Assert.Matches("<span>Tolerância</span>\\s*<strong>± \\d+\\.\\d+</strong>", html);
    }
}

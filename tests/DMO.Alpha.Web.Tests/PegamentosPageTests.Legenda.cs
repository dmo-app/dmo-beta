using System.Net;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // Deep sweep de 18/10/2026, regra 98: a legenda do mapa pintava os círculos
    // a #0183D7, uma cor que o desenho nunca usa (os círculos são da cor do
    // componente, vermelhos ou cinzentos), e faltava o cinzento de «não
    // avaliável». A legenda mostra as cores que o mapa usa.
    [Fact]
    public async Task MapaDeLimites_ALegendaMostraAsCoresDoDesenho()
    {
        SeedUser("4431", "Pegamentos Legenda", "provider-peg-legenda", "PegLegenda1!");
        using var client = await LoginClientAsync("4431", "PegLegenda1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var posted = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", contraCostura: "69.90", gargalo: "70.00");
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        var html = await GetPageHtmlAsync(client, url);
        var svg = Regex.Match(html, "<svg[^>]*aria-label=\"Mapa de limites dos Pegamentos\"[\\s\\S]*?</svg>").Value;
        // Mais largo e centrado na página (Diogo, 23/10/2026): o desenho estica na horizontal, a altura fica; o PDF continua com 1000.
        Assert.Contains("width=\"1700\" height=\"245\" viewBox=\"0 0 1700 245\"", svg);
        Assert.Contains("<path d='M115 44H1665V206H115Z'", svg);
        var legenda = Regex.Match(html, "<div class=\"peg-map-legend\">([\\s\\S]*?)</div>").Groups[1].Value;
        Assert.NotEmpty(svg);
        Assert.NotEmpty(legenda);
        Assert.DoesNotContain("0183D7", html, StringComparison.OrdinalIgnoreCase);

        static string[] Cores(string texto) => Regex.Matches(texto, "#[0-9a-fA-F]{6}\\b").Select(m => m.Value.ToLowerInvariant()).Distinct().ToArray();
        var coresDaLegenda = Cores(legenda);
        // Cada cor de círculo, de losango e de nominal desenhada está na legenda...
        var desenhadas = Regex.Matches(svg, "<(?:circle|polygon|line)\\b[^>]*(?:fill|stroke)='(#[0-9a-fA-F]{6})'")
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Where(c => c is not "#dbe5ef" and not "#cbd5e1") // a grelha e as faixas de fundo
            .Distinct().ToArray();
        Assert.NotEmpty(desenhadas);
        Assert.All(desenhadas, cor => Assert.Contains(cor, coresDaLegenda));
        // ... e a legenda não tem cores que o desenho não use: as dos
        // componentes, o vermelho de «fora» e o cinzento de «não avaliável».
        var doDesenho = new[]
        {
            DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.ComponentColor("CM"),
            DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.ComponentColor("BQ"),
            DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.ComponentColor("MF"),
            DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.AlertColor,
            DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.NotEvaluableColor,
        }.Select(c => c.ToLowerInvariant()).ToArray();
        Assert.All(coresDaLegenda, cor => Assert.Contains(cor, doDesenho));
        var texto = WebUtility.HtmlDecode(legenda);
        Assert.Contains(DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.NotEvaluableColor.ToLowerInvariant(), coresDaLegenda);
        Assert.Contains("não avaliável", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.AlertColor.ToLowerInvariant(), coresDaLegenda);
        Assert.DoesNotContain("var(--dmo-brand-600)", legenda, StringComparison.Ordinal);
    }
}

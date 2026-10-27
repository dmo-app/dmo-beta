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
        // No ecrã o mapa passou a HTML, com texto legível (Diogo, 27/10/2026);
        // o PDF continua com o desenho de PegamentosLimitMap.Render.
        Assert.DoesNotContain("viewBox=\"0 0 1700 245\"", html);
        Assert.Contains("aria-label=\"Mapa de limites dos Pegamentos", html);
        var legenda = Regex.Match(html, "<div class=\"peg-map-legend\">([\\s\\S]*?)</div>").Groups[1].Value;
        Assert.NotEmpty(legenda);
        Assert.DoesNotContain("0183D7", html, StringComparison.OrdinalIgnoreCase);

        static string[] Cores(string texto) => Regex.Matches(texto, "#[0-9a-fA-F]{6}\\b").Select(m => m.Value.ToLowerInvariant()).Distinct().ToArray();
        var coresDaLegenda = Cores(legenda);
        // Cada cor de componente desenhada está na legenda...
        var desenhadas = Regex.Matches(html, "--peg-cor:(#[0-9a-fA-F]{6})")
            .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray();
        Assert.NotEmpty(desenhadas);
        Assert.All(desenhadas, cor => Assert.Contains(cor, coresDaLegenda));
        // ... e a legenda não tem cores que não sejam as dos componentes.
        var doDesenho = new[] { "CM", "BQ", "MF" }
            .Select(c => DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.ComponentColor(c).ToLowerInvariant()).ToArray();
        Assert.All(coresDaLegenda, cor => Assert.Contains(cor, doDesenho));
        // «Fora» e «não avaliável» continuam na legenda, com as cores do tema.
        var texto = WebUtility.HtmlDecode(legenda);
        Assert.Contains("não avaliável", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fora", texto, StringComparison.Ordinal);
        Assert.Contains("peg-gr-fora", legenda, StringComparison.Ordinal);
        Assert.Contains("peg-gr-na", legenda, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--dmo-brand-600)", legenda, StringComparison.Ordinal);
    }
}

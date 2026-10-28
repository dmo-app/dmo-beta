using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // As linhas compatíveis do «Registar ferramenta» (B1, B2, C1…) são um botão por linha, que se escolhe
    // com um clique (DMO-405): a página dá uma caixa por linha, com o código da linha como texto, e o
    // tests/visual/cobertura_c_browser.py mede no Chromium as cores do rato por cima e da escolhida.
    [Fact]
    public async Task Registar_DaUmaCaixaPorLinhaComOCodigoDela_EVaiParaOBrowser()
    {
        SeedUser("4497", "Linhas Compatíveis", "provider-cobc-linhas", "FerrLinhas1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Machines.AddRange(new Machine { Code = "B1" }, new Machine { Code = "B2" }, new Machine { Code = "C1" });
            db.SaveChanges();
        }

        using var client = await LoginClientAsync("4497", "FerrLinhas1!");
        var html = await GetPageHtmlAsync(client, IndexPath + "?criar=1");

        var criar = html[html.IndexOf("id=\"ferr-create-form\"", StringComparison.Ordinal)..];
        Assert.Equal(new[] { "B1", "B2", "C1" },
            System.Text.RegularExpressions.Regex.Matches(criar, "<label class=\"ferr-check\">\\s*<input type=\"checkbox\" name=\"machine_ids\"[^>]*/>\\s*<span>([^<]+)</span>")
                .Select(m => m.Groups[1].Value).OrderBy(c => c, StringComparer.Ordinal).ToArray());
        ExportarParaOBrowser.Gravar("cobertura-c-ferramentas.html", html);
    }
}

using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

// A página nunca fica presa à altura da janela (Diogo, 30/10/2026: «não deixa
// ver a tabela … desde que fizeram lock à página»). O menos scroll possível,
// mas a página desliza sempre quando não cabe (DMO-690, A-0456).
public sealed class PaginasDeslizamCssTests
{
    [Fact]
    public void NenhumaPaginaFicaPresaAAlturaDaJanela()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "wwwroot", "css");
        // Regras de uma linha (as das páginas); o painel lateral tem o corpo a deslizar.
        var trava = new Regex(@"[^{}\n]*\{[^}\n]*\bheight:(100dvh|100vh|calc\(100vh[^)]*\))[^}\n]*overflow:(hidden|clip)[^}\n]*\}|[^{}\n]*\{[^}\n]*overflow:(hidden|clip)[^}\n]*\bheight:(100dvh|100vh|calc\(100vh[^)]*\))[^}\n]*\}");
        var presas = Directory.GetFiles(dir, "*.css")
            .SelectMany(f => trava.Matches(File.ReadAllText(f)).Select(m => Path.GetFileName(f) + ": " + m.Value))
            // Estas duas já deslizam por dentro (corpo da página e folha dos Pegamentos).
            .Where(r => !r.Contains(".dmo-pagina-topo") && !r.Contains(".peg-folha"))
            .ToList();
        Assert.Empty(presas);
    }
}

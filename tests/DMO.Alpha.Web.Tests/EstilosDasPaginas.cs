using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

// Limpeza do CSS, parte 2 (A-0498): os blocos <style> das páginas passaram
// para wwwroot/css/paginas/. Junta ao HTML (ou ao .cshtml) o texto das folhas
// que a página liga, para os testes continuarem a ver as mesmas regras.
internal static class EstilosDasPaginas
{
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web");
    }

    public static string Juntar(string html)
    {
        var folhas = Regex.Matches(html, "/css/paginas/([a-z0-9-]+)\\.css")
            .Select(m => m.Groups[1].Value).Distinct()
            .Select(n => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "paginas", n + ".css")));
        return html + "\n" + string.Join("\n", folhas);
    }
}

using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Regra 7 do código (docs/architecture/REGRAS-DE-CODIGO.md; sweep de
/// 19/10/2026, A-4): as páginas recebem as dependências pelo construtor. Nada
/// de <c>new AlgumaQuery(db)</c>, <c>HttpContext.RequestServices</c> ou uma
/// página a criar outra página para lhe usar um método: o que duas páginas
/// partilham passa para um serviço. Os filtros e atributos (Services,
/// Authorization) não têm construtor e ficam de fora.
/// </summary>
public sealed class RegraDasDependenciasTests
{
    private static readonly Regex Escondida = new(
        @"ActivatorUtilities|RequestServices|new\((_db|db)\b|new [A-Za-z.]+(Query|Handler|Builder)\(",
        RegexOptions.Compiled);

    [Fact]
    public void AsPaginas_RecebemAsDependenciasPeloConstrutor()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var web = Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web");
        var ficheiros = new[] { "Pages", "ViewComponents" }
            .SelectMany(pasta => Directory.EnumerateFiles(Path.Combine(web, pasta), "*.cs", SearchOption.AllDirectories))
            .ToArray();
        Assert.NotEmpty(ficheiros);

        var escondidas = ficheiros
            .SelectMany(f => File.ReadLines(f).Select((linha, i) => (f, linha, i)))
            .Where(x => Escondida.IsMatch(x.linha))
            .Select(x => $"{Path.GetRelativePath(web, x.f)}:{x.i + 1}: {x.linha.Trim()}")
            .ToArray();
        Assert.True(escondidas.Length == 0, "Dependências escondidas:\n" + string.Join("\n", escondidas));
    }
}

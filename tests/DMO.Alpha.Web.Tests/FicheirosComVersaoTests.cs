using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Depois de uma versão nova, o browser usa logo o CSS e o JS novos
/// (vistoria ao frontend F1, 27/10/2026): cada folha e cada script levam a
/// marca de versão (asp-append-version, ?v=…), os ficheiros com a marca
/// guardam-se um ano e os outros perguntam sempre ao servidor se mudaram.
/// Antes, três folhas do login e o script do Peso iam sem marca e sem
/// Cache-Control, e podiam ficar os antigos até um Ctrl+F5.
/// </summary>
public sealed class FicheirosComVersaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FicheirosComVersaoTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web");
    }

    [Fact]
    public void CadaFolhaEScriptDasPaginas_LevaAMarcaDeVersao()
    {
        var etiqueta = new Regex("<link[^>]*rel=\"stylesheet\"[^>]*>|<script[^>]*\\ssrc=[^>]*>");
        var semMarca = new List<string>();
        var vistas = 0;
        foreach (var ficheiro in Directory.EnumerateFiles(Path.Combine(WebRoot(), "Pages"), "*.cshtml", SearchOption.AllDirectories))
        {
            foreach (Match m in etiqueta.Matches(File.ReadAllText(ficheiro)))
            {
                vistas++;
                if (!m.Value.Contains("asp-append-version=\"true\"", StringComparison.Ordinal))
                    semMarca.Add($"{Path.GetFileName(ficheiro)}: {m.Value}");
            }
        }

        Assert.True(vistas > 20, "o teste tem de encontrar as folhas e os scripts das páginas");
        Assert.True(semMarca.Count == 0, "sem marca de versão:\n" + string.Join("\n", semMarca));
    }

    [Fact]
    public async Task OLogin_PedeCadaFolhaComAMarcaDeVersao()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/login");

        var folhas = Regex.Matches(html, "<link rel=\"stylesheet\" href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains(folhas, href => href.StartsWith("/css/dmo-design.css?v=", StringComparison.Ordinal));
        Assert.All(folhas, href => Assert.Contains("?v=", href, StringComparison.Ordinal));
    }

    [Fact]
    public async Task UmFicheiroComMarca_GuardaSeUmAno_ESemMarca_PerguntaSempre()
    {
        using var client = _factory.CreateClient();

        var comMarca = await client.GetAsync("/css/dmo-design.css?v=abc");
        var semMarca = await client.GetAsync("/css/dmo-design.css");
        var logo = await client.GetAsync("/0_ASSET_LOGO.png");

        Assert.True(comMarca.IsSuccessStatusCode);
        Assert.Equal(TimeSpan.FromDays(365), comMarca.Headers.CacheControl!.MaxAge);
        Assert.True(comMarca.Headers.CacheControl.Public);
        Assert.Contains("immutable", comMarca.Headers.CacheControl.Extensions.Select(e => e.Name));
        Assert.True(semMarca.Headers.CacheControl!.NoCache);
        Assert.Null(semMarca.Headers.CacheControl.MaxAge);
        Assert.True(logo.Headers.CacheControl!.NoCache);
    }
}

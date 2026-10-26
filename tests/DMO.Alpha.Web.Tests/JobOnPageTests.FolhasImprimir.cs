namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AS FOLHAS DO IMPRIMIR (Diogo, 26/10/2026): o «Imprimir» do Job On é um menu
/// que escolhe a folha (Ficha de Artigo, Trabalho de Equipa, ou o ecrã). As
/// folhas vêm escondidas na página com os dados do Job On, e o browser
/// imprime-as (nunca PDF no servidor). Quem só tem «Job On ver» não as recebe.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn()
    {
        SeedUser("4581", "Chefe imprimir", "provider-folha-imprimir-1", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4581", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        Assert.Contains("data-jof-imprimir>Imprimir</button>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"menu-list\" data-jof-imprimir-lista hidden>", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-imprimir-folha=\"ficha\">Ficha de Artigo</button>", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-imprimir-folha=\"equipa\">Trabalho de Equipa</button>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"jof-impressao\" data-jof-impressao hidden>", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-folha=\"ficha\"", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-folha=\"equipa\"", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"jfi-ref\">5810T188</div>", html, StringComparison.Ordinal);
        Assert.Contains("<span>BOQUILHA (BQ)</span><span>Lote 21</span>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><td>Contra-molde</td><td>5809</td><td>1</td><td></td></tr>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_Imprimir_SoVer_NaoRecebeAsFolhas()
    {
        SeedUserSoVer("4582", "provider-folha-imprimir-2", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4582", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        Assert.DoesNotContain("data-jof-impressao", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-imprimir", html, StringComparison.Ordinal);
    }
}

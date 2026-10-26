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
        // Um só clique imprime as duas folhas juntas: sem lista para escolher (Diogo, 26/10/2026).
        Assert.DoesNotContain("data-jof-imprimir-lista", html, StringComparison.Ordinal);
        Assert.DoesNotContain("COMENTÁRIOS", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"jof-impressao\" data-jof-impressao hidden>", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-folha=\"ficha\"", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-folha=\"equipa\"", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"jfi-ref\">5810T188</div>", html, StringComparison.Ordinal);
        Assert.Contains("<span>BOQUILHA (BQ)</span><span>Lote 21</span>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><td>CM · Contra-molde</td><td>5809</td><td>1</td><td></td></tr>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_DecisoesDoDiogo_ComponentesSeparadosAnComLoteCaloteUnicaSemComentarios()
    {
        SeedUser("4583", "Chefe decisoes", "provider-folha-imprimir-3", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4583", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        // Imprimir é um clique: sem lista de folhas nem «Esta folha (ecrã)».
        Assert.DoesNotContain("data-jof-imprimir-lista", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Esta folha (ecrã)", html, StringComparison.Ordinal);
        // Sem a caixa «Comentários» na impressão.
        Assert.DoesNotContain("COMENTÁRIOS", html, StringComparison.Ordinal);
        // O AN é um componente à parte, com lote próprio, no ecrã e na impressão.
        Assert.Contains("data-jof-familia=\"AN\"", html, StringComparison.Ordinal);
        Assert.Contains("<span>ANEL (AN)</span>", html, StringComparison.Ordinal);
        // Cada componente na sua linha do Trabalho de Equipa.
        foreach (var linha in new[] { "CM · Contra-molde", "TP · Tampão", "PU · Punção", "ARR · Arrefecedor", "MF · Molde final", "BQ · Boquilha", "AN · Anel", "CS · Cab. de sopro", "PI · Pinça" })
            Assert.Contains("<td>" + linha + "</td>", html, StringComparison.Ordinal);
        // Calote e bacia do TP são a mesma coisa: só «Calote» na impressão; calibres só com «Qtd.».
        Assert.DoesNotContain("· Bacia ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>QTD máq.</th><th>Qtd.</th>", html, StringComparison.Ordinal);
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

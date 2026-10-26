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
        Assert.Contains("<span>BQ · Boquilha</span><span>Lote 21</span>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><td>CM · Contra-molde</td><td>5809</td><td></td><td>Lote 1</td></tr>", html, StringComparison.Ordinal);
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
        Assert.Contains("<span>AN · Anel</span>", html, StringComparison.Ordinal);
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

    [Fact]
    public async Task Folha_Imprimir_SaemAsQuatroFolhasSemComentarios()
    {
        SeedUser("4584", "Chefe quatro folhas", "provider-folha-imprimir-4", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4584", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        // Um clique imprime as 4 folhas, nesta ordem (Diogo, 26/10/2026; Excel dos documentos).
        var ordem = new[] { "ficha", "equipa", "preparador", "pincas" }
            .Select(f => html.IndexOf($"data-jof-folha=\"{f}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(ordem, i => Assert.True(i > 0));
        Assert.Equal(ordem.OrderBy(i => i), ordem);
        Assert.Contains("FOR 05 MOL 006 (pt)", html, StringComparison.Ordinal);
        Assert.Contains("Pinças - DMO", html, StringComparison.Ordinal);
        // Sem comentários na impressão: nem «Notas do Job On» nem «Tratar».
        Assert.DoesNotContain("NOTAS DO JOB ON", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Tratar:</b>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_Imprimir_CamposDaFabrica_EquipaComLadosECalibresComQtd_TpSoComCalote()
    {
        SeedUser("4585", "Chefe fabrica", "provider-folha-imprimir-5", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4585", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        // Trabalho de Equipa como o da fábrica: os dois lados e os 6 calibres cada um com «Qtd.».
        Assert.Contains("<span>LADO DO CONTRA-MOLDE</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span>LADO DO MOLDE FINAL</span>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><th>Nível</th><th>CM</th><th>MF</th><th>Tampão</th><th>CS</th><th>Pinças</th></tr>", html, StringComparison.Ordinal);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(html, "<td>Qtd\\.:</td>").Count);
        // Ficha de Artigo: Dados, Valor e Tol. separados, e cada componente com rótulos.
        Assert.Contains("<tr class=\"jfi-cab\"><th>Dados</th><td>Valor</td><td>Tol.</td></tr>", html, StringComparison.Ordinal);
        Assert.Contains("<tr><th>Calote</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<tr><th>Bacia</th>", html, StringComparison.Ordinal);
        // Preparador com os dados da fábrica (Loc., Qtd.) e unidades sempre em «mm».
        Assert.Contains("Loc.: ____", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" MM<", html, StringComparison.Ordinal);
    }
}

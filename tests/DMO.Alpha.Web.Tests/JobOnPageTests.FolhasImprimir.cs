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
        // Referência e lote dentro da Ferramenta (Diogo, 26/10/2026 22:02), e no cartão da Equipa.
        Assert.Contains("<strong>T188</strong> <span>Lote <i>21</i></span>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"jfc-nome\"><span>CM</span> Contra-Molde</div>", html, StringComparison.Ordinal);
        Assert.Contains("<b>5809</b> <span class=\"jfc-lote jfc-lote-d\">Lote <b>1</b></span>", html, StringComparison.Ordinal);
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
        Assert.Contains("<b>Anel</b>", html, StringComparison.Ordinal);
        // Cada componente na sua linha do Trabalho de Equipa.
        // Como no PDF preto e branco (Diogo, 27/10/2026): sigla e nome, FF à parte, sem o AN.
        foreach (var (sigla, nome) in new[] { ("CM", "Contra-Molde"), ("TP", "Tampão"), ("PU", "Punção"), ("TR", "Tub. Refrigeração"), ("MF", "Molde Final"), ("FF", "Fundo Final"), ("BQ", "Boquilha"), ("CS", "Cab. Enchimento"), ("PI", "Pinça") })
            Assert.Contains("<div class=\"jfc-nome\"><span>" + sigla + "</span> " + nome + "</div>", html, StringComparison.Ordinal);
        // Calote e bacia do TP são a mesma coisa: só «Calote» na impressão; calibres só com «Qtd.».
        Assert.DoesNotContain("· Bacia ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>QTD máq.</th><th>Qtd.</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_Imprimir_SaemAsQuatroFolhasComoAsDaFabrica()
    {
        SeedUser("4584", "Chefe quatro folhas", "provider-folha-imprimir-4", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4584", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        // Job-ON Moldes (2 vezes), Trabalho de Equipa e Ficha de Artigo, por esta ordem (Diogo, 26/10/2026 21:10).
        var ordem = System.Text.RegularExpressions.Regex.Matches(html, "data-jof-folha=\"(moldes|equipa|ficha)\"")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[] { "moldes", "moldes", "equipa", "ficha" }, ordem);
        Assert.DoesNotContain("Preparador", html, StringComparison.Ordinal);
        // Sem Horas nem Dia da semana, sem Quantidade e sem Forro nas folhas (Diogo, 26/10/2026).
        Assert.DoesNotContain("<span>Horas</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Dia da semana</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forro", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<div class=\"jfb-lado\"><span>Quantidade</span>", html, StringComparison.Ordinal);
        // Os dados dos componentes usam a tabela do sistema (dmo-table).
        Assert.Contains("<table class=\"dmo-table jfb-dados\">", html, StringComparison.Ordinal);
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

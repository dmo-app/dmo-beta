using System.Net;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoComparacaoPageTests
{
    [Fact]
    public async Task Comparacao_AbreNoResumoDaProducao_SemSeparadorProprio()
    {
        // «Esta comparação passa a existir dentro do controlo_id; assim quando
        // abrir o resumo posso criar uma comparação com o peso que lá está em
        // relação àquela produção e outra qualquer» (Diogo, 21/10/2026).
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4688", "Peso Operador", "provider-cmp-resumo", "PesoCmpResumo1!");
        using var client = await LoginClientAsync("4688", "PesoCmpResumo1!");

        // O cartão do Peso do Resumo tem «Comparar», que abre a comparação
        // desta produção.
        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.Contains(
            $"<a class=\"dmo-button\" href=\"/Controlo/Peso?controlo_id={controloId}&amp;comparacao=1\" aria-label=\"Comparar o Peso desta produção\">Comparar</a>",
            resumo);

        // A comparação abre com a produção e o Peso dela escolhidos; a
        // produção fica fixa: as CM tiradas comparam-se sempre com a produção
        // de onde saíram (Diogo, 27/10/2026).
        var comparacao = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");
        Assert.Contains($"<input type=\"hidden\" name=\"jobon_id\" value=\"{jobOnId}\" />", comparacao);
        Assert.Contains($"<input type=\"hidden\" name=\"peso_inicial\" value=\"{pesoInicialId}\" />", comparacao);
        Assert.DoesNotContain("id=\"cmpReferencia\"", comparacao);

        // Sem separador «Comparação» na barra: o Resumo da produção fica ativo.
        var nav = System.Text.RegularExpressions.Regex.Match(comparacao,
            "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
        Assert.DoesNotContain("Comparação", WebUtility.HtmlDecode(nav));
        Assert.Matches($"<a [^>]*href=\"/Controlo/Resumo\\?jobon_id={jobOnId}\"[^>]*class=\"[^\"]*active", nav);
    }
}

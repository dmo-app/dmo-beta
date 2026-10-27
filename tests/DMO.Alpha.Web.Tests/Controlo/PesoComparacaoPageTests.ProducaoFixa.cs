using System.Net;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoComparacaoPageTests
{
    /// <summary>
    /// As CM tiradas comparam-se sempre com a produção de onde saíram (Diogo,
    /// 27/10/2026: «é sempre comparado à mesma produção que saiu»): aberta
    /// pelo Resumo, a comparação fica na produção do controlo, sem os campos
    /// da referência e da produção; com um só Peso, sem escolha do Peso inicial.
    /// </summary>
    [Fact]
    public async Task PeloResumo_AProducaoFicaFixa_SemReferenciaNemProducao()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4691", "Peso Operador", "provider-cmp-fixa-1", "PesoCmpFixa1!");
        using var client = await LoginClientAsync("4691", "PesoCmpFixa1!");

        // A referência escrita não troca a produção.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1&referencia=9999");

        Assert.Contains("data-peso-producao-fixa", html);
        Assert.Contains("<strong>1075C142 · 202601 · B1</strong>", html);
        Assert.DoesNotContain("id=\"cmpReferencia\"", html);
        Assert.DoesNotContain("id=\"cmpProducao\"", html);
        Assert.DoesNotContain("id=\"cmpInicial\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"producao_fixa\" value=\"1\" />", html);
        Assert.Contains($"<input type=\"hidden\" name=\"jobon_id\" value=\"{jobOnId}\" />", html);
        Assert.Contains($"<input type=\"hidden\" name=\"peso_inicial\" value=\"{pesoInicialId}\" />", html);
        Assert.Contains("Contra-molde a pesar", html);

        // Pelo Histórico ou pelo Peso, sem controlo, continua a escolha da produção.
        var livre = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}");
        Assert.Contains("id=\"cmpReferencia\"", livre);
        Assert.DoesNotContain("data-peso-producao-fixa", livre);
    }

    [Fact]
    public async Task ProducaoFixa_SegueNoCompararENoGuardar()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4692", "Peso Operador", "provider-cmp-fixa-2", "PesoCmpFixa2!");
        using var client = await LoginClientAsync("4692", "PesoCmpFixa2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var comparar = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("producao_fixa", "1"),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, comparar.StatusCode);
        var html = await comparar.Content.ReadAsStringAsync();
        Assert.Contains("data-peso-producao-fixa", html);
        Assert.DoesNotContain("id=\"cmpReferencia\"", html);

        var guardar = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("producao_fixa", "1"),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "198.64"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        var location = guardar.Headers.Location!.ToString();
        Assert.Contains("producao_fixa=1", location, StringComparison.Ordinal);

        var guardada = await GetPageHtmlAsync(client, location);
        Assert.Contains("data-peso-producao-fixa", guardada);
        Assert.DoesNotContain("id=\"cmpReferencia\"", guardada);
    }
}

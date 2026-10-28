namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // DMO-482 (Diogo, 27/10/2026): a página dos Pegamentos tem as leituras e
    // o mapa lado a lado e já não tem os passos «1 · Contexto / 2 · Medições
    // / 3 · Mapa».
    [Fact]
    public async Task Folha_TemAsLeiturasEOMapa_SemOsPassosContextoMedicoesMapa()
    {
        SeedUser("4880", "Pegamentos Operador", "provider-peg-passos", "PegPassos1!");
        using var client = await LoginClientAsync("4880", "PegPassos1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }

        var html = await GetPageHtmlAsync(client, url);

        var folha = html.IndexOf("class=\"peg-folha\"", StringComparison.Ordinal);
        Assert.True(folha >= 0, "a página não tem a folha com as leituras e o mapa");
        Assert.True(html.IndexOf("class=\"peg-folha-leituras\"", folha, StringComparison.Ordinal) > folha,
            "as leituras estão dentro da folha");
        Assert.DoesNotContain("1 · Contexto", html, StringComparison.Ordinal);
        Assert.DoesNotContain("2 · Medições", html, StringComparison.Ordinal);
        Assert.DoesNotContain("3 · Mapa", html, StringComparison.Ordinal);
        Assert.DoesNotContain("1 · Contexto", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }
}

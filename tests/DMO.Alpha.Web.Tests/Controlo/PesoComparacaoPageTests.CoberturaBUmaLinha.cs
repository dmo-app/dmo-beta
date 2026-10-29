using System.Net;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoComparacaoPageTests
{
    // DMO-407: a comparação abre com uma só linha de leitura e, depois de
    // comparar, o formulário volta com as linhas que o operador escreveu.
    [Fact]
    public async Task Comparacao_ComecaComUmaSoLinhaDeLeitura_EDepoisDeCompararVoltaComAsEscritas()
    {
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4879", "Peso Operador", "provider-cmp-uma-linha", "PesoCmpUma1!");
        using var client = await LoginClientAsync("4879", "PesoCmpUma1!");

        var aberta = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}");

        Assert.Contains("data-cmp-readings", aberta);
        Assert.Equal(1, Regex.Matches(aberta, "<div class=\"peso-leitura-row\" data-cmp-row>").Count);
        Assert.Contains("data-cmp-add-row", aberta);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(aberta)!;
        var response = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("cm_subject", "31"), ("water_weight", "150"),
            ("cm_subject", "44"), ("water_weight", "160"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comparada = await response.Content.ReadAsStringAsync();

        Assert.Equal(3, Regex.Matches(comparada, "<div class=\"peso-leitura-row\" data-cmp-row>").Count);
    }
}

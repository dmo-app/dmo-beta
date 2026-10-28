namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // DMO-460 (Diogo, 27/10/2026): a página de registo do Peso, dos Pegamentos
    // e das Punções tem o «Voltar» ao Resumo da produção, antes do título.
    [Theory]
    [InlineData("Peso", "<h1 id=\"peso-title\">Peso</h1>")]
    [InlineData("Pegamentos", "<h1 id=\"peg-title\">Pegamentos</h1>")]
    [InlineData("Puncoes", "<h1 id=\"pu-title\">PU NNPB</h1>")]
    public async Task PaginasDeRegisto_TemOVoltarAoResumoDaProducao_AntesDoTitulo(string pagina, string titulo)
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4874", "provider-voltar-resumo", "VoltarResumo1!");
        using var client = await LoginClientAsync("4874", "VoltarResumo1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/{pagina}?controlo_id={controloId}");

        var voltar = $"href=\"/Controlo/Resumo?jobon_id={jobOnId}\" data-dmo-voltar=\"resumo\"";
        Assert.Contains(voltar + " data-dmo-voltar-ignorar=\"/Controlo/Peso /Controlo/Pegamentos /Controlo/Puncoes\" data-dmo-voltar-ao-resumo>Voltar</a>", html);
        Assert.True(PosicaoNoHtml.Posicao(html, voltar) < PosicaoNoHtml.Posicao(html, titulo),
            "o «Voltar» vem antes do título da página");
    }
}

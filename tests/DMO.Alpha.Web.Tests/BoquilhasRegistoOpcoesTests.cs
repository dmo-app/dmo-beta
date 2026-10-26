using Xunit;

namespace DMO.Alpha.Web.Tests;

// O registo das Boquilhas: «Movimentos» nas «Opções», ao lado de «Alterar
// lote» (Diogo, 26/10/2026), abre o separador Histórico já filtrado por esta
// BQ e lote. Só a BQ, não o MF: a mesma BQ e lote serve várias referências totais
// (5447QU501, 5531QU501) e o Histórico mostra cada uma.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal()
    {
        await SeedToolAsync("BQ-USOS-1", "QU501", "3");
        await SeedToolAsync("BQ-USOS-2", "QU501", "4");
        var bq = await SeedProductionAsync("BQ-USOS-1", "5447QU501", "PN-880001", "B6");
        var trace = await SeedTraceAsync("BQ-USOS-1", bq.Id);
        var outro = await SeedTraceAsync("BQ-USOS-2");
        var outroMf = await SeedProductionAsync("BQ-USOS-1", "5531QU501", "PN-880002", "B7");
        var traceOutroMf = await SeedTraceAsync("BQ-USOS-1", outroMf.Id);
        await SeedMovementAsync(traceOutroMf.Id, "entrada", 1);
        await SeedMovementAsync(trace.Id, "saida", 2);
        await SeedMovementAsync(outro.Id, "saida", 5);

        SeedUser("4196", "Registo Usos", "provider-registo-usos", "RegistoUsos1!");
        using var client = await LoginClientAsync("4196", "RegistoUsos1!");

        var page = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.Contains("id=\"bqRegisterFacts\"", page);
        Assert.Contains("<summary class=\"dmo-button\" aria-label=\"Opções desta BQ e lote\"><span class=\"dmo-caret\" aria-hidden=\"true\"></span>Opções</summary>", page);
        Assert.Contains("<a class=\"dmo-options-item bq-register-history-link\" href=\"/Boquilhas?historico=1&amp;bq_ferramenta=BQ-USOS-1\" title=\"Movimentos desta BQ e lote\">Movimentos</a>", page);
        Assert.Contains("id=\"bqAlterarLoteAbrir\" data-dmo-dialog=\"bq-alterar-lote\">Alterar lote</button>", page);
        Assert.DoesNotContain("Histórico BQ", page);
        Assert.DoesNotContain("Onde esta boquilha foi usada", page);

        var historico = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&bq_ferramenta=BQ-USOS-1");
        // A BQ escolhida preenche os filtros à vista (Diogo, 24/10/2026).
        Assert.Matches("<input id=\"pesquisa\"[^>]*value=\"QU501\"", historico);
        Assert.Matches("<input id=\"lote\" name=\"lote\" value=\"3\"", historico);
        Assert.DoesNotContain("name=\"bq_ferramenta\"", historico);
        Assert.Contains("2 movimentos", historico);
        Assert.Contains("5447QU501", historico);
        Assert.Contains("5531QU501", historico);
        Assert.DoesNotContain("Tirar filtro", historico);
        Assert.Contains("<a class=\"btn\" href=\"/Boquilhas?historico=1&amp;vista=grupos\">Limpar filtros</a>", historico);

        // Os dois campos escritos à mão dão o mesmo filtro; outro lote, nenhum movimento.
        var escrito = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=QU501&lote=3");
        Assert.Contains("2 movimentos", escrito);
        var outroLote = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=QU501&lote=9");
        Assert.Contains("0 movimentos", outroLote);
    }

    [Fact]
    public async Task Registo_AlterarLote_ABqPassaParaOLoteEscolhido_EORegistoAbreNoLoteNovo()
    {
        await SeedToolAsync("BQ-LOTE-165", "T288", "165");
        await SeedToolAsync("BQ-LOTE-167", "T288", "167");
        await SeedToolAsync("BQ-LOTE-OUTRA", "T173", "9");
        var bq = await SeedProductionAsync("BQ-LOTE-165", "9262T288", "PN-880101", "C2");
        var trace = await SeedTraceAsync("BQ-LOTE-165", bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 2);

        SeedUser("4197", "Registo Lote", "provider-registo-lote", "RegistoLote1!");
        using var client = await LoginClientAsync("4197", "RegistoLote1!");

        var page = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.Contains("<option value=\"BQ-LOTE-167\">Lote 167</option>", page);
        Assert.DoesNotContain("BQ-LOTE-OUTRA", page);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)!;

        var response = await client.PostAsync("/Boquilhas?handler=AlterarLote", HtmlFormHelpers.BuildFormPost(
            ("alterar_bq_id", bq.Id.ToString()), ("tool_id", "BQ-LOTE-167"), ("__RequestVerificationToken", token)));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var destino = response.Headers.Location!.ToString();
        Assert.Contains("lote_alterado=167", destino);

        var novo = await GetPageHtmlAsync(client, destino);
        Assert.Contains("A BQ passou para o lote 167. Os movimentos já registados ficam no lote antigo.", novo);
        Assert.Contains("BQ T288 · Lote 167", novo);

        // Os movimentos ficam no lote antigo; nele já não há lote a alterar.
        Assert.Single(await ReadMovementsAsync(trace.Id));
        var antigo = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.DoesNotContain("bqAlterarLoteAbrir", antigo);
    }
}

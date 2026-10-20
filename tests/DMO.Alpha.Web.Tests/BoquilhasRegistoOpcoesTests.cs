using Xunit;

namespace DMO.Alpha.Web.Tests;

// O registo das Boquilhas: «Opções» abre o «Histórico BQ» (Diogo, 19/10/2026,
// antes «Onde esta boquilha foi usada»), o separador Histórico já filtrado por
// esta BQ e lote.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task Registo_Opcoes_HistoricoBq_AbreOHistoricoFiltradoPorEstaBqELote()
    {
        await SeedToolAsync("BQ-USOS-1", "QU501", "3");
        await SeedToolAsync("BQ-USOS-2", "QU501", "4");
        var bq = await SeedProductionAsync("BQ-USOS-1", "5447QU501", "PN-880001", "B6");
        var trace = await SeedTraceAsync("BQ-USOS-1", bq.Id);
        var outro = await SeedTraceAsync("BQ-USOS-2");
        await SeedMovementAsync(trace.Id, "saida", 2);
        await SeedMovementAsync(outro.Id, "saida", 5);

        SeedUser("4196", "Registo Usos", "provider-registo-usos", "RegistoUsos1!");
        using var client = await LoginClientAsync("4196", "RegistoUsos1!");

        var page = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.Contains("id=\"bqRegisterFacts\"", page);
        Assert.Contains("href=\"/Boquilhas?historico=1&amp;bq_ferramenta=BQ-USOS-1\">Histórico BQ</a>", page);
        Assert.DoesNotContain("Onde esta boquilha foi usada", page);

        var historico = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&bq_ferramenta=BQ-USOS-1");
        Assert.Contains("BQ QU501 · Lote 3", historico);
        Assert.Contains("<input type=\"hidden\" name=\"bq_ferramenta\" value=\"BQ-USOS-1\" />", historico);
        Assert.Contains("1 movimento(s)", historico);
        Assert.Contains("title=\"Tirar o filtro da BQ\">Tirar filtro</a>", historico);
    }
}

using Xunit;

namespace DMO.Alpha.Web.Tests;

// O registo das Boquilhas: o link «Histórico» à vista, fora das «Opções»
// (Diogo, 22/10/2026), abre o separador Histórico já filtrado por esta BQ e
// lote. Só a BQ, não o MF: a mesma BQ e lote serve várias referências totais
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
        Assert.Contains("<a class=\"bq-register-history-link\" href=\"/Boquilhas?historico=1&amp;bq_ferramenta=BQ-USOS-1\" title=\"Histórico desta BQ e lote\">Histórico</a>", page);
        Assert.DoesNotContain("Histórico BQ", page);
        Assert.DoesNotContain("dmo-options", page);
        Assert.DoesNotContain("Onde esta boquilha foi usada", page);

        var historico = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&bq_ferramenta=BQ-USOS-1");
        Assert.Contains("BQ QU501 · Lote 3", historico);
        Assert.Contains("<input type=\"hidden\" name=\"bq_ferramenta\" value=\"BQ-USOS-1\" />", historico);
        Assert.Contains("2 movimento(s)", historico);
        Assert.Contains("5447QU501", historico);
        Assert.Contains("5531QU501", historico);
        Assert.Contains("title=\"Tirar o filtro da BQ\">Tirar filtro</a>", historico);
    }
}

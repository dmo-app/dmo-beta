namespace DMO.Alpha.Web.Tests;

public partial class BoquilhasRegistoPageTests
{
    // DMO-522: o registo de uma BQ com produção mostra o «BQ · lote» e o resto dos
    // cartões; a página vai para o Chromium (tests/visual/cobertura_a2_browser.py),
    // que mede se algum dado sai cortado.
    [Fact]
    public async Task RegistoComProducao_MostraBqLoteEOsCartoesDaProducao_ParaOBrowser()
    {
        await SeedToolAsync("BQ-A2-522", "RGT935", "1");
        var bq = await SeedProductionAsync("BQ-A2-522", "ZQREF-522", "PN-552201", "B2");
        var trace = await SeedTraceAsync("BQ-A2-522", bqContextId: bq.Id);
        SeedUser("4522", "Registo Produção", "provider-registo-522", "Registo522!");
        using var client = await LoginClientAsync("4522", "Registo522!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        Assert.Contains("<span>BQ · lote</span><strong>RGT935 · Lote 1</strong>", html);
        Assert.Contains("<span>Referência</span><strong>ZQREF-522</strong>", html);
        Assert.Contains("PN-552201", html);
        ExportarParaOBrowser.Gravar("cobertura-a2-bq-registo-producao.html", html);
    }
}

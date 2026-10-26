namespace DMO.Alpha.Web.Tests;

public partial class BoquilhasRegistoPageTests
{
    // OS CARTÕES DO REGISTO ABREM O QUE MOSTRAM (Diogo, 26/10/2026: «meter no BQ
    // lote um link para ir à ferramenta em questão … metemos um link no resto que
    // leva ao Job On», «o jobon_id, não o Job On geral»): o cartão inteiro é o
    // link; a ficha só para quem tem o módulo Ferramentas.
    [Fact]
    public async Task Cartoes_BqLoteAbreAFicha_ORestoAbreOJobOnDaProducao()
    {
        await SeedToolAsync("BQ-CARTAO-1", "RGT961", "9");
        var bq = await SeedProductionAsync("BQ-CARTAO-1", "ZQREF-61", "PN-556001", "B2");
        var trace = await SeedTraceAsync("BQ-CARTAO-1", bqContextId: bq.Id);
        SeedUser("4131", "Cartões chefe", "provider-registo-cartoes-1", "Cartoes1!", withFerramentas: true);
        using var chefe = await LoginClientAsync("4131", "Cartoes1!");

        var html = await GetPageHtmlAsync(chefe, TraceUrl(trace.Id));

        Assert.Contains("<a class=\"dmo-fact dmo-fact-link\" href=\"/Ferramentas/Ficha?tool_id=BQ-CARTAO-1\" title=\"Abrir a ficha desta BQ\"><span>BQ · lote</span><strong>RGT961 · Lote 9</strong></a>", html);
        foreach (var rotulo in new[] { "Referência", "Produção", "Início", "Fim" })
        {
            Assert.Matches("<a class=\"dmo-fact dmo-fact-link[^\"]*\" href=\"/JobOn\\?jobon_id=\\d+#lightFicha\" title=\"Abrir o Job On desta produção\"><span>" + rotulo + "</span>", html);
        }

        SeedUser("4132", "Cartões operador", "provider-registo-cartoes-2", "Cartoes2!");
        using var operador = await LoginClientAsync("4132", "Cartoes2!");
        var semFicha = await GetPageHtmlAsync(operador, TraceUrl(trace.Id));
        Assert.Contains("<div class=\"dmo-fact\"><span>BQ · lote</span><strong>RGT961 · Lote 9</strong></div>", semFicha);
        Assert.DoesNotContain("href=\"/Ferramentas/Ficha?tool_id=BQ-CARTAO-1\"", semFicha);
    }
}

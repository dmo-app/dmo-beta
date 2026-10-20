using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class BoquilhasRegistoPageTests
{
    // Teste do agente de 19/10/2026 (T1f15887 e Tcb734f2): numa BQ posta a
    // zero, o texto «Posta a zero por…» (agora «Metida a zero por…») da Discrepância ficava sempre aberto,
    // fora do cartão e da página. A página vai para o Chromium
    // (tests/visual/nok_agente_browser.py), que vê a dica fechada até ao
    // rato e a página sem scroll lateral.
    [Fact]
    public async Task PostaAZero_ADicaDaDiscrepanciaFicaNaEtiqueta_ParaOBrowser()
    {
        await SeedToolAsync("BQ-ZERO-4743", "RGT743", "1");
        var trace = await SeedTraceAsync("BQ-ZERO-4743");
        await SeedDatedMovementAsync(trace.Id, BqMovementType.Entrada, 4, DateTime.UtcNow.AddDays(-2));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var guardado = await db.BqRepairTraces.SingleAsync(t => t.Id == trace.Id);
            guardado.DiscrepancyResetAtUtc = DateTime.UtcNow.AddHours(-1);
            guardado.DiscrepancyResetByNameFrozen = "Ana Zero Com Um Nome Bastante Comprido";
            await db.SaveChangesAsync();
        }
        SeedUser("4743", "Ana Zero", "provider-zero-browser", "ZeroBrowser1!");
        using var client = await LoginClientAsync("4743", "ZeroBrowser1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Matches("<span>Discrepância acumulada<span class=\"dmo-help\"[^>]*>[\\s\\S]*?Metida a zero por Ana Zero Com Um Nome Bastante Comprido em", html);
        ExportarParaOBrowser.Gravar("boquilha-zero.html", html);
    }
}

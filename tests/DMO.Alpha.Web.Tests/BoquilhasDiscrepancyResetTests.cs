using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Pôr a discrepância a zero (Diogo, 17/10/2026: começar a registar a meio
/// das produções): um clique no valor abre a janela, «Pôr a zero» grava quem e
/// quando, e os movimentos ficam como estão. E a data e hora que se mantêm
/// depois de gravar um movimento com data antiga.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task DiscrepancyReset_ClickOpensTheWindow_AndPuttingToZeroKeepsTheMovements()
    {
        await SeedToolAsync("BQ-RESET-1", "RGT801", "1");
        var trace = await SeedTraceAsync("BQ-RESET-1");
        var past = DateTime.UtcNow.AddDays(-2);
        await SeedDatedMovementAsync(trace.Id, BqMovementType.Entrada, 4, past);
        using (var scope = _factory.Services.CreateScope())
        {
            // A discrepância como o handler a deixaria: 4 entram sem saída.
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var movement = await db.BqMovements.SingleAsync(m => m.BqRepairTraceId == trace.Id);
            movement.Discrepancy = -4;
            await db.SaveChangesAsync();
        }
        SeedUser("4801", "Ana Reset", "provider-reset-1", "ResetPass1!");
        using var client = await LoginClientAsync("4801", "ResetPass1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains($"href=\"/Boquilhas?trace_id={trace.Id}&amp;acerto=1#bqMetrics\"", html);
        Assert.Contains(">-4</a></strong>", html);
        Assert.DoesNotContain("id=\"bqDiscrepancyReset\"", html);

        var opened = await GetPageHtmlAsync(client, $"{PagePath}?trace_id={trace.Id}&acerto=1");
        Assert.Contains("id=\"bqDiscrepancyReset\"", opened);
        Assert.Contains("Meter a discrepância a zero?", opened);
        Assert.Contains("Meter a zero</button>", opened);
        Assert.Contains($"href=\"/Boquilhas?trace_id={trace.Id}#bqMetrics\">Cancelar</a>", opened);

        var response = await PostCorrectionAsync(
            client, "ResetDiscrepancy", TraceUrl(trace.Id), ("trace_id", trace.Id.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}#bqMetrics", response.Headers.Location!.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var saved = await db.BqRepairTraces.AsNoTracking().SingleAsync(t => t.Id == trace.Id);
            Assert.NotNull(saved.DiscrepancyResetAtUtc);
            Assert.Equal("Ana Reset", saved.DiscrepancyResetByNameFrozen);
        }

        // O movimento e o saldo da linha ficam; a acumulada passa a 0, sem link.
        var movementAfter = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(-4, movementAfter.Discrepancy);
        var after = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("<strong>0</strong>", after);
        Assert.DoesNotContain("acerto=1", after);
        Assert.Contains("Metida a zero por Ana Reset em", after);
    }

    [Fact]
    public async Task DiscrepancyReset_WithoutDiscrepancy_ShowsNoLink()
    {
        await SeedToolAsync("BQ-RESET-2", "RGT802", "2");
        var trace = await SeedTraceAsync("BQ-RESET-2");
        await SeedMovementAsync(trace.Id, "saida", 3);
        SeedUser("4802", "Sem Reset", "provider-reset-2", "ResetPass2!");
        using var client = await LoginClientAsync("4802", "ResetPass2!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("<span>Discrepância acumulada</span><strong>—</strong>", html);
        Assert.DoesNotContain("acerto=1", html);

        // Pedir a janela sem discrepância não a abre.
        var asked = await GetPageHtmlAsync(client, $"{PagePath}?trace_id={trace.Id}&acerto=1");
        Assert.DoesNotContain("id=\"bqDiscrepancyReset\"", asked);
    }

    [Fact]
    public async Task PostMovement_WithAnOldDate_KeepsThatDateInTheForm_WithNowGoesBackToNow()
    {
        await SeedToolAsync("BQ-KEEP-1", "RGT803", "3");
        var trace = await SeedTraceAsync("BQ-KEEP-1");
        SeedUser("4803", "Guias", "provider-keep-1", "KeepPass1!");
        using var client = await LoginClientAsync("4803", "KeepPass1!");

        var old = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 2, "2026-09-14T09:30");
        Assert.Equal(HttpStatusCode.Redirect, old.StatusCode);
        var location = old.Headers.Location!.OriginalString;
        Assert.Contains("data_antiga=2026-09-14T09%3A30", location);
        var form = await GetPageHtmlAsync(client, location);
        Assert.Contains("value=\"2026-09-14T09:30\"", form);

        // No uso diário (a data pré-preenchida com o agora) o form volta ao agora.
        var now = DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var daily = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 2, now);
        Assert.Equal(HttpStatusCode.Redirect, daily.StatusCode);
        Assert.DoesNotContain("data_antiga", daily.Headers.Location!.OriginalString);
    }
}

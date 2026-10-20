using System.Net;
using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O registo ordenado pela data e hora reais, seja qual for a ordem em que as
/// guias são lançadas (Diogo, 17/10/2026): «primeiro meto 20 sair 15 entrar
/// às 12:45, se depois meter 10 sair 14 entrar às 8:21 do mesmo dia».
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task GuiasOutOfOrder_TableFollowsTheRealTime_AndTheSaidaCountsFirstAtTheSameTime()
    {
        await SeedToolAsync("BQ-ORDEM-1", "RGT701", "1");
        var trace = await SeedTraceAsync("BQ-ORDEM-1");
        SeedUser("4701", "Ordem", "provider-ordem-1", "OrdemPass1!");
        using var client = await LoginClientAsync("4701", "OrdemPass1!");

        // A guia das 12:45 com a entrada lançada antes da saída; depois a das 08:21.
        foreach (var (type, quantity, at) in new[]
                 {
                     ("entrada", 15, "2026-10-12T12:45"),
                     ("saida", 20, "2026-10-12T12:45"),
                     ("saida", 10, "2026-10-12T08:21"),
                     ("entrada", 14, "2026-10-12T08:21")
                 })
        {
            var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, type, quantity, at);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var movements = await ReadMovementsAsync(trace.Id);
        // 08:21: saem 10, entram 14 (4 a mais); 12:45: a saída de 20 conta antes da entrada de 15.
        Assert.Equal(-4, movements.Single(m => m.Quantity == 14).Discrepancy);
        Assert.Null(movements.Single(m => m.Quantity == 15).Discrepancy);

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("<span>Em reparação</span><strong>5</strong>", html);
        var table = html[html.IndexOf("id=\"bqTraceMovements\"", StringComparison.Ordinal)..];
        table = table[..table.IndexOf("</table>", StringComparison.Ordinal)];
        // Os mais recentes no topo: 12:45 entrada, 12:45 saída, 08:21 entrada, 08:21 saída.
        var order = new[] { "<td>15</td>", "<td>20</td>", "<td>14</td>", "<td>10</td>" }
            .Select(cell => table.IndexOf(cell, StringComparison.Ordinal))
            .ToList();
        Assert.All(order, index => Assert.True(index >= 0));
        Assert.Equal(order.OrderBy(index => index), order);
        Assert.Equal(BqMovementType.Entrada, movements.Single(m => m.Quantity == 15).Type);
    }
}

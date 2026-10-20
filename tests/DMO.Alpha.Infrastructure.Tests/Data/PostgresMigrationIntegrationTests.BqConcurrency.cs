using System.Data.Common;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Movimentos gravados ao mesmo tempo no mesmo registo (relatório de 18/10/2026:
/// depois de uma saída de 10, duas entradas simultâneas de 10 ficavam com
/// discrepância total 0 em vez de −10). Cada gravação recalcula o registo
/// inteiro; sem trinco, as duas liam a mesma saída e ambas a davam por
/// devolvida.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private const int ConcurrencyRounds = 8;

    [SkippableFact]
    public async Task BqMovements_EntradasSimultaneas_RecalculamADiscrepancia()
    {
        SkipWithoutConnection();

        await using (var setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
        }

        var marker = Guid.NewGuid().ToString("N")[..10];
        var toolId = $"BQ-CONC-{marker}";
        var occurred = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var traceIds = new List<int>();

        await using (var db = CreateContext())
        {
            db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
            await db.SaveChangesAsync();
        }

        try
        {
            for (var round = 0; round < ConcurrencyRounds; round++)
            {
                // Um registo por ronda: a corrida não aparece sempre à primeira.
                await using (var db = CreateContext())
                {
                    var trace = new BqRepairTrace { ToolReferenceFrozen = "9389T194", ToolLotFrozen = "1" };
                    db.BqRepairTraces.Add(trace);
                    await db.SaveChangesAsync();
                    traceIds.Add(trace.Id);
                    db.BqMovements.Add(new BqMovement
                    {
                        BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 10,
                        OccurredAtUtc = occurred, CreatedAtUtc = occurred
                    });
                    // O registo não resolvido ancora na ferramenta; só um por ferramenta,
                    // por isso a âncora usa-se só para o registo desta ronda.
                    trace.ToolId = toolId;
                    await db.SaveChangesAsync();
                }

                var traceId = traceIds[^1];
                using var start = new SemaphoreSlim(0, 2);
                async Task<RegisterBqMovementOutcome> RegisterAsync(int minutes)
                {
                    await using var db = CreateContext();
                    await db.Database.OpenConnectionAsync();
                    await start.WaitAsync();
                    return await new RegisterBqMovementHandler(db, new NoRouteSettings()).HandleAsync(
                        new RegisterBqMovement(traceId, "entrada", 10, null, occurred.AddMinutes(minutes), null));
                }

                var first = RegisterAsync(10);
                var second = RegisterAsync(20);
                start.Release(2);
                var outcomes = await Task.WhenAll(first, second);
                Assert.All(outcomes, o => Assert.Equal(RegisterBqMovementResult.Success, o.Result));

                await using (var db = CreateContext())
                {
                    var discrepancies = await db.BqMovements.AsNoTracking()
                        .Where(m => m.BqRepairTraceId == traceId)
                        .Select(m => m.Discrepancy ?? 0)
                        .ToListAsync();
                    Assert.Equal(-10, discrepancies.Sum());

                    await db.BqRepairTraces.Where(t => t.Id == traceId)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.ToolId, (string?)null));
                }
            }
        }
        finally
        {
            await using var db = CreateContext();
            await db.BqMovements.Where(m => traceIds.Contains(m.BqRepairTraceId)).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => traceIds.Contains(t.Id)).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// Deep sweep (18/10/2026): os primeiros movimentos de duas produções com
    /// a mesma BQ, ao mesmo tempo, com um registo pré-produção dessa BQ por
    /// associar. Os dois pedidos liam o mesmo registo e o segundo, depois do
    /// trinco, associava-o à sua produção por cima do primeiro: ficava um só
    /// registo com os movimentos das duas máquinas e a outra produção sem
    /// registo. Agora o segundo fica com um registo próprio.
    /// </summary>
    [SkippableFact]
    public async Task BqMovements_PrimeirosMovimentosDeDuasProducoesAoMesmoTempo_CadaUmaFicaComOSeuRegisto()
    {
        SkipWithoutConnection();
        var marker = "SW" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var bq = "BQ-" + marker;
        int m1, m2, c1, c2, preTraceId;
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            int j1, j2;
            (m1, j1) = await SeedJobOnAsync(db, marker + "A");
            (m2, j2) = await SeedJobOnAsync(db, marker + "B");
            db.Tools.Add(new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
            await db.SaveChangesAsync();
            var context1 = new BqContext { JobOnId = j1, ToolId = bq };
            var context2 = new BqContext { JobOnId = j2, ToolId = bq };
            db.BqContexts.AddRange(context1, context2);
            var preProduction = new BqRepairTrace { ToolId = bq, ToolReferenceFrozen = "9389T194", ToolLotFrozen = "1" };
            db.BqRepairTraces.Add(preProduction);
            await db.SaveChangesAsync();
            (c1, c2, preTraceId) = (context1.Id, context2.Id, preProduction.Id);
        }

        try
        {
            var rendezvous = new Rendezvous();
            async Task<RegisterBqMovementOutcome> RegisterAsync(int contextId, int quantity)
            {
                await using var db = CreateContextWith(new AtForUpdate(rendezvous));
                return await new RegisterBqMovementHandler(db, new NoRouteSettings())
                    .HandleForContextAsync(new RegisterBqMovementForContext(contextId, "saida", quantity));
            }

            var outcomes = await Task.WhenAll(RegisterAsync(c1, 5), RegisterAsync(c2, 7));
            Assert.All(outcomes, o => Assert.Equal(RegisterBqMovementResult.Success, o.Result));

            await using var check = CreateContext();
            var traces = await check.BqRepairTraces.AsNoTracking()
                .Where(t => t.BqContextId == c1 || t.BqContextId == c2)
                .Select(t => new
                {
                    t.Id,
                    t.BqContextId,
                    Quantities = check.BqMovements.Where(m => m.BqRepairTraceId == t.Id).Select(m => m.Quantity).ToList()
                })
                .ToListAsync();
            Assert.Equal(2, traces.Count);
            Assert.Equal(new[] { 5 }, traces.Single(t => t.BqContextId == c1).Quantities);
            Assert.Equal(new[] { 7 }, traces.Single(t => t.BqContextId == c2).Quantities);
            Assert.Contains(traces, t => t.Id == preTraceId);
        }
        finally
        {
            await using var db = CreateContext();
            var traceIds = db.BqRepairTraces
                .Where(t => t.ToolId == bq || t.BqContextId == c1 || t.BqContextId == c2).Select(t => t.Id);
            await db.BqMovements.Where(m => traceIds.Contains(m.BqRepairTraceId)).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => t.ToolId == bq || t.BqContextId == c1 || t.BqContextId == c2).ExecuteDeleteAsync();
            await db.BqContexts.Where(c => c.ToolId == bq).ExecuteDeleteAsync();
            await DeleteJobOnAsync(db, m1);
            await DeleteJobOnAsync(db, m2);
            await db.Tools.Where(t => t.ToolId == bq).ExecuteDeleteAsync();
        }
    }

    /// <summary>Faz os dois pedidos chegarem juntos ao «FOR UPDATE» do trinco do registo.</summary>
    private sealed class Rendezvous
    {
        private int _count;
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _count) >= 2) _all.TrySetResult();
            try { await _all.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { }
        }
    }

    private sealed class AtForUpdate(Rendezvous rendezvous) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE")) await rendezvous.ArriveAsync();
            return result;
        }
    }
}

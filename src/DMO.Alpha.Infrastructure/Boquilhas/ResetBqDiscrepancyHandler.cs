using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Põe a zero a discrepância acumulada de um registo (Diogo, 17/10/2026:
/// «quando começar a usar a app agora vou iniciar já no meio das produções e
/// não tenho forma de ter certeza que vai bater bem»). Grava só o momento e
/// quem: os movimentos, o saldo de cada linha e o «Em reparação» não mudam.
/// A discrepância acumulada passa a contar só os movimentos com data real
/// depois desse momento.
/// </summary>
public sealed class ResetBqDiscrepancyHandler : IResetBqDiscrepancyHandler
{
    private readonly DmoDbContext _db;
    private readonly TimeProvider _clock;

    public ResetBqDiscrepancyHandler(DmoDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ResetBqDiscrepancyResult> HandleAsync(
        ResetBqDiscrepancy command,
        CancellationToken cancellationToken = default)
    {
        var trace = await _db.BqRepairTraces
            .FirstOrDefaultAsync(t => t.Id == command.BqRepairTraceId, cancellationToken);
        if (trace is null)
        {
            return ResetBqDiscrepancyResult.TraceNotFound;
        }

        trace.DiscrepancyResetAtUtc = _clock.GetUtcNow().UtcDateTime;
        trace.DiscrepancyResetByNameFrozen = BqActorName.Freeze(command.ResetBy);
        await _db.SaveChangesAsync(cancellationToken);
        return ResetBqDiscrepancyResult.Success;
    }
}

using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Apaga um movimento de um registo, qualquer que seja a data real, e recalcula as discrepâncias dos que ficam. Na mesma gravação deixa o rasto
/// em BqMovementDeletions (os factos do movimento, quem apagou e quando);
/// sem identidade apaga na mesma.
/// </summary>
public sealed class DeleteBqMovementHandler : IDeleteBqMovementHandler
{
    private readonly DmoDbContext _db;
    private readonly TimeProvider _clock;

    public DeleteBqMovementHandler(DmoDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DeleteBqMovementResult> HandleAsync(
        DeleteBqMovement command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BqTraceLock.AcquireAsync(
            _db, command.BqRepairTraceId, cancellationToken);
        var traceMovements = await _db.BqMovements
            .Where(m => m.BqRepairTraceId == command.BqRepairTraceId)
            .ToListAsync(cancellationToken);
        var movement = traceMovements.FirstOrDefault(m => m.Id == command.BqMovementId);
        if (movement is null)
        {
            return DeleteBqMovementResult.MovementNotFound;
        }

        _db.BqMovementDeletions.Add(new BqMovementDeletion
        {
            BqMovementId = movement.Id,
            BqRepairTraceId = movement.BqRepairTraceId,
            Type = movement.Type,
            Quantity = movement.Quantity,
            OccurredAtUtc = movement.OccurredAtUtc,
            MachineCodeFrozen = movement.MachineCodeFrozen,
            DeletedByNameFrozen = BqActorName.Freeze(command.DeletedBy),
            DeletedAtUtc = _clock.GetUtcNow().UtcDateTime
        });
        _db.BqMovements.Remove(movement);
        traceMovements.Remove(movement);
        // A conta é da BQ em todas as produções (Diogo, 28/10/2026); só se gravam os deste registo.
        var registo = await _db.BqRepairTraces.AsNoTracking().FirstAsync(t => t.Id == command.BqRepairTraceId, cancellationToken);
        BqDiscrepancyFold.Apply(traceMovements.Concat(await BqDiscrepancyFold.DosOutrosRegistosAsync(_db, registo, cancellationToken)));
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return DeleteBqMovementResult.Success;
    }
}

using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Apaga um dos movimentos mais recentes de um registo (data real, depois id)
/// e recalcula as discrepâncias dos que ficam. Na mesma gravação deixa o rasto
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
        var traceMovements = await _db.BqMovements
            .Where(m => m.BqRepairTraceId == command.BqRepairTraceId)
            .ToListAsync(cancellationToken);
        var movement = traceMovements.FirstOrDefault(m => m.Id == command.BqMovementId);
        if (movement is null)
        {
            return DeleteBqMovementResult.MovementNotFound;
        }

        var mostRecent = traceMovements
            .OrderByDescending(m => m.OccurredAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(DeleteBqMovement.MostRecentLimit);
        if (!mostRecent.Contains(movement))
        {
            return DeleteBqMovementResult.NotAmongMostRecent;
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
        BqDiscrepancyFold.Apply(traceMovements);
        await _db.SaveChangesAsync(cancellationToken);

        return DeleteBqMovementResult.Success;
    }
}

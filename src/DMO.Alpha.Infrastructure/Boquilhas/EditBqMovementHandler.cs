using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Corrige o tipo, a quantidade e a data/hora reais de um movimento gravado.
/// A máquina e o reparador congelados ficam como estão; as discrepâncias do
/// registo são recalculadas pela data real. Só recusa o que partiria os dados;
/// uma hora inexistente na mudança de hora passa para a hora seguinte.
/// Guarda quem corrigiu e quando (a última correção); sem identidade grava na mesma.
/// </summary>
public sealed class EditBqMovementHandler : IEditBqMovementHandler
{
    private readonly DmoDbContext _db;
    private readonly TimeProvider _clock;

    public EditBqMovementHandler(DmoDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<EditBqMovementOutcome> HandleAsync(
        EditBqMovement command,
        CancellationToken cancellationToken = default)
    {
        var traceMovements = await _db.BqMovements
            .Where(m => m.BqRepairTraceId == command.BqRepairTraceId)
            .ToListAsync(cancellationToken);
        var movement = traceMovements.FirstOrDefault(m => m.Id == command.BqMovementId);
        if (movement is null)
        {
            return new EditBqMovementOutcome(EditBqMovementResult.MovementNotFound);
        }

        if (command.MovementType is not (RegisterBqMovementHandler.Saida
            or RegisterBqMovementHandler.Entrada
            or RegisterBqMovementHandler.EntradaSemReparacao))
        {
            return new EditBqMovementOutcome(EditBqMovementResult.UnknownMovementType);
        }

        if (command.Quantity < 1)
        {
            return new EditBqMovementOutcome(EditBqMovementResult.InvalidQuantity);
        }

        // Uma hora que cai no salto da mudança de hora (março) passa para a
        // hora seguinte e a página avisa; nunca recusa (decisão do proprietário
        // de 16/10/2026: avisar em vez de bloquear, desde que não parta nada).
        DateTime? shiftedLocal = null;
        if (command.OccurredAtLocal is DateTime local)
        {
            var validLocal = DmoBusinessTime.NextValidLocal(local, out var shifted);
            if (shifted)
            {
                shiftedLocal = validLocal;
            }

            movement.OccurredAtUtc = DmoBusinessTime.ToUtc(validLocal);
        }

        movement.Type = BqMovementTypeTokens.FromStorage(command.MovementType);
        movement.Quantity = command.Quantity;
        movement.CorrectedByNameFrozen = BqActorName.Freeze(command.CorrectedBy);
        movement.CorrectedAtUtc = _clock.GetUtcNow().UtcDateTime;
        BqDiscrepancyFold.Apply(traceMovements);
        await _db.SaveChangesAsync(cancellationToken);

        return new EditBqMovementOutcome(
            EditBqMovementResult.Success,
            new RegisteredBqMovement(
                movement.Id,
                movement.BqRepairTraceId,
                command.MovementType,
                movement.Quantity,
                movement.Discrepancy),
            shiftedLocal);
    }
}

using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Corrige o tipo, a quantidade e a data/hora reais de um movimento gravado.
/// A máquina e o reparador congelados ficam como estão; as discrepâncias do
/// registo são recalculadas pela data real. Só recusa o que partiria os dados;
/// uma data real no futuro grava e a página avisa; uma hora inexistente na
/// mudança de hora passa para a hora seguinte.
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
        await using var transaction = await BqTraceLock.AcquireAsync(
            _db, command.BqRepairTraceId, cancellationToken);
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

        if (command.ChangesAn && command.AnQuantity < 0)
        {
            return new EditBqMovementOutcome(EditBqMovementResult.InvalidAnQuantity);
        }

        // Um movimento só de AN (Diogo, 28/10/2026: «Registar sem Job On» com
        // a peça AN) tem 0 BQ; sem AN, as BQ continuam a ser pelo menos 1.
        var anDepois = command.ChangesAn ? command.AnQuantity : movement.AnQuantity;
        if (command.Quantity < 0 || (command.Quantity == 0 && anDepois is not > 0))
        {
            return new EditBqMovementOutcome(EditBqMovementResult.InvalidQuantity);
        }

        // Uma hora que cai no salto da mudança de hora (março) passa para a
        // hora seguinte e a página avisa; nunca recusa (decisão do proprietário
        // de 16/10/2026: avisar em vez de bloquear, desde que não parta nada).
        var now = _clock.GetUtcNow();
        DateTime? shiftedLocal = null;
        var occurredInTheFuture = false;
        if (command.OccurredAtLocal is DateTime local)
        {
            var validLocal = DmoBusinessTime.NextValidLocal(local, out var shifted);
            if (shifted)
            {
                shiftedLocal = validLocal;
            }

            var occurredAtUtc = DmoBusinessTime.ToUtc(validLocal);

            // Uma data real no futuro grava e a página avisa (Diogo,
            // 19/10/2026: «as datas têm de ser livres»; na beta.241 era recusada).
            occurredInTheFuture = BqMovementOccurrence.IsInTheFuture(occurredAtUtc, now);
            movement.OccurredAtUtc = occurredAtUtc;
        }

        movement.Type = BqMovementTypeTokens.FromStorage(command.MovementType);
        movement.Quantity = command.Quantity;
        if (command.ChangesAn)
        {
            movement.AnQuantity = command.AnQuantity is > 0 ? command.AnQuantity : null;
        }

        movement.CorrectedByNameFrozen = BqActorName.Freeze(command.CorrectedBy);
        movement.CorrectedAtUtc = now.UtcDateTime;
        // A conta é da BQ em todas as produções (Diogo, 28/10/2026); só se gravam os deste registo.
        var registo = await _db.BqRepairTraces.AsNoTracking().FirstAsync(t => t.Id == command.BqRepairTraceId, cancellationToken);
        BqDiscrepancyFold.Apply(traceMovements.Concat(await BqDiscrepancyFold.DosOutrosRegistosAsync(_db, registo, cancellationToken)));
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new EditBqMovementOutcome(
            EditBqMovementResult.Success,
            new RegisteredBqMovement(
                movement.Id,
                movement.BqRepairTraceId,
                command.MovementType,
                movement.Quantity,
                movement.Discrepancy),
            shiftedLocal,
            occurredInTheFuture);
    }
}

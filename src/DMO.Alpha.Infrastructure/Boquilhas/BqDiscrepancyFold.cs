using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A regra matched/unmatched sobre todos os movimentos de um registo, pela
/// data real: as saídas acrescentam ao que está fora; cada retorno devolve
/// só a porção explicável e o excedente fica anotado como discrepância negativa.
/// </summary>
public static class BqDiscrepancyFold
{
    /// <summary>Data real, depois id; um movimento ainda por gravar (Id 0) fica no fim entre datas iguais.</summary>
    public static IOrderedEnumerable<BqMovement> Chronological(IEnumerable<BqMovement> movements) =>
        movements
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => movement.Id == 0 ? int.MaxValue : movement.Id);

    /// <summary>
    /// Recalcula a discrepância de todos os movimentos (null quando não há
    /// excedente) e devolve a quantidade legitimamente fora no fim.
    /// </summary>
    public static int Apply(IEnumerable<BqMovement> movements)
    {
        var outstanding = 0;
        foreach (var movement in Chronological(movements))
        {
            if (movement.Type == BqMovementType.Saida)
            {
                outstanding += movement.Quantity;
                movement.Discrepancy = null;
                continue;
            }

            var matched = Math.Min(movement.Quantity, outstanding);
            outstanding -= matched;
            var unmatched = movement.Quantity - matched;
            movement.Discrepancy = unmatched > 0 ? -unmatched : null;
        }

        return outstanding;
    }
}

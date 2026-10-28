using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A regra matched/unmatched sobre todos os movimentos de uma BQ, pela data
/// real: as saídas acrescentam ao que está fora; cada retorno devolve só a
/// porção explicável e o excedente fica anotado como discrepância negativa.
/// A conta é da BQ em todas as produções (Diogo, 28/10/2026: «só deve criar
/// saldo quando entra mais do que saiu»): uma BQ que sai num Job On e volta
/// registada noutro não fica a mais.
/// </summary>
public static class BqDiscrepancyFold
{
    /// <summary>
    /// Data real; à mesma hora a saída conta antes da entrada (Diogo,
    /// 17/10/2026: uma guia com saída e entrada à mesma hora); depois id, e um
    /// movimento ainda por gravar (Id 0) fica no fim entre iguais.
    /// </summary>
    public static IOrderedEnumerable<BqMovement> Chronological(IEnumerable<BqMovement> movements) =>
        movements
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => SaidaFirst(movement.Type))
            .ThenBy(movement => movement.Id == 0 ? int.MaxValue : movement.Id);

    /// <summary>A posição do tipo entre movimentos à mesma hora: a saída primeiro.</summary>
    public static int SaidaFirst(BqMovementType type) => type == BqMovementType.Saida ? 0 : 1;

    /// <summary>
    /// Se um movimento conta para a discrepância acumulada depois de posta a
    /// zero: data real posterior ao zero. O campo da hora só tem minutos, por
    /// isso um movimento gravado depois do zero com a hora desse mesmo minuto
    /// também conta (teste do agente de 19/10/2026, T616eb4d: pôr a zero às
    /// 18:20:56 e gravar logo a seguir com 18:20 contava como antes do zero).
    /// </summary>
    public static bool CountsAfterReset(DateTime occurredAtUtc, DateTime createdAtUtc, DateTime resetAtUtc) =>
        occurredAtUtc > resetAtUtc
        || (createdAtUtc > resetAtUtc
            && occurredAtUtc >= resetAtUtc.AddTicks(-(resetAtUtc.Ticks % TimeSpan.TicksPerMinute)));

    /// <summary>
    /// Recalcula a discrepância de todos os movimentos (null quando não há
    /// excedente) e devolve a quantidade legitimamente fora no fim.
    /// </summary>
    public static int Apply(IEnumerable<BqMovement> movements)
    {
        var ordered = Chronological(movements).ToList();
        var (discrepancies, outstanding) = Fold(ordered.Select(m => (m.Type == BqMovementType.Saida, m.Quantity)).ToList());
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Discrepancy = discrepancies[i];
        }

        return outstanding;
    }

    /// <summary>
    /// A conta sobre quantidades já em ordem cronológica (a das BQ ou a dos
    /// AN): a discrepância de cada movimento (negativa, só nas entradas; null
    /// sem excedente) e a quantidade fora no fim.
    /// </summary>
    public static (int?[] Discrepancies, int Outstanding) Fold(IReadOnlyList<(bool Saida, int Quantity)> movements)
    {
        var discrepancies = new int?[movements.Count];
        var outstanding = 0;
        for (var i = 0; i < movements.Count; i++)
        {
            var (saida, quantity) = movements[i];
            if (saida)
            {
                outstanding += quantity;
                continue;
            }

            var matched = Math.Min(quantity, outstanding);
            outstanding -= matched;
            var unmatched = quantity - matched;
            discrepancies[i] = unmatched > 0 ? -unmatched : null;
        }

        return (discrepancies, outstanding);
    }

    /// <summary>
    /// Os movimentos dos outros registos da mesma BQ (as outras produções e
    /// o registo sem Job On), só para a conta: não são gravados.
    /// </summary>
    public static async Task<List<BqMovement>> DosOutrosRegistosAsync(
        DmoDbContext db, BqRepairTrace trace, CancellationToken cancellationToken)
    {
        var toolId = trace.ToolId;
        if (toolId is null && trace.BqContextId is int contextId)
        {
            toolId = await db.BqContexts.AsNoTracking()
                .Where(c => c.Id == contextId)
                .Select(c => c.ToolId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (toolId is null)
        {
            return [];
        }

        var traceId = trace.Id;
        var outros = RegistosDaBq(db, toolId).Where(id => id != traceId);
        return await db.BqMovements.AsNoTracking()
            .Where(m => outros.Contains(m.BqRepairTraceId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Os ids dos registos de uma BQ (sem Job On e de cada produção).</summary>
    public static IQueryable<int> RegistosDaBq(DmoDbContext db, string toolId) =>
        from t in db.BqRepairTraces
        join c in db.BqContexts on t.BqContextId equals (int?)c.Id into cs
        from c in cs.DefaultIfEmpty()
        where t.ToolId == toolId || (c != null && c.ToolId == toolId)
        select t.Id;
}

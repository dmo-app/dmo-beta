using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// O resumo diário da grelha a partir dos movimentos já filtrados: uma entrada
/// por dia local, máquina e registo, com as contagens e as somas de quantidade
/// por tipo. Os movimentos sem máquina não entram na grelha (não têm coluna).
/// </summary>
public static class BqDailySummary
{
    public static IReadOnlyList<BqDailySummaryEntry> FromMovements(IEnumerable<BqHistoryMovementItem> movements)
    {
        var entrada = BqMovementType.Entrada.ToString();
        var saida = BqMovementType.Saida.ToString();
        var semReparacao = BqMovementType.EntradaSemReparacao.ToString();

        return movements
            .Where(movement => movement.MachineCode is not null)
            .OrderBy(movement => movement.OccurredAtUtc)
            .GroupBy(movement => (
                Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(movement.OccurredAtUtc, DmoBusinessTime.Zone)),
                Machine: movement.MachineCode!,
                Trace: movement.BqRepairTraceId))
            .Select(group =>
            {
                var first = group.First();
                return new
                {
                    group.Key,
                    First = first.OccurredAtUtc,
                    Entry = new BqDailySummaryEntry(
                        group.Key.Day,
                        group.Key.Machine,
                        group.Key.Trace,
                        first.ToolReference,
                        first.ToolLot,
                        first.ProductionNumber,
                        first.Reference,
                        group.Count(m => m.MovementType == entrada),
                        group.Count(m => m.MovementType == saida),
                        group.Count(m => m.MovementType == semReparacao),
                        group.Where(m => m.MovementType == entrada).Sum(m => m.Quantity),
                        group.Where(m => m.MovementType == saida).Sum(m => m.Quantity),
                        group.Where(m => m.MovementType == semReparacao).Sum(m => m.Quantity))
                };
            })
            .OrderBy(x => x.Key.Day)
            .ThenBy(x => x.Key.Machine)
            .ThenBy(x => x.First)
            .Select(x => x.Entry)
            .ToList();
    }
}

namespace DMO.Alpha.Core.Boquilhas;

// A máquina é a do movimento. Mesmo trace em duas máquinas gera dois grupos;
// produções com o mesmo número e lotes diferentes nunca se misturam.
public sealed record BqHistoryMovementGroup(
    string? MachineCode, int TraceId, string? ProductionNumber, string? Reference,
    string? ToolReference, string? ToolLot, IReadOnlyList<BqHistoryMovementItem> Movements)
{
    public static IReadOnlyList<BqHistoryMovementGroup> From(IReadOnlyList<BqHistoryMovementItem> movements) =>
        movements.GroupBy(m => new { m.MachineCode, m.BqRepairTraceId })
            .OrderBy(g => g.Key.MachineCode, StringComparer.Ordinal)
            .ThenBy(g => g.Key.BqRepairTraceId)
            .Select(g => new BqHistoryMovementGroup(g.Key.MachineCode, g.Key.BqRepairTraceId,
                g.First().ProductionNumber, g.First().Reference, g.First().ToolReference, g.First().ToolLot,
                g.OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.MovementId).ToList()))
            .ToList();
}

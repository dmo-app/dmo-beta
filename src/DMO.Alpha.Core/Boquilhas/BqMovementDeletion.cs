namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Rasto de um movimento apagado (o Owner 2026-10-16: registar quem apagou e
/// quando). A linha do movimento sai da tabela BqMovements (sem soft delete);
/// aqui fica uma cópia dos factos que tinha — o id original, o registo, o tipo,
/// a quantidade, a data real e a máquina congelada — com quem apagou e quando.
/// Sem chaves estrangeiras: é evidência, não uma relação viva.
/// </summary>
public sealed class BqMovementDeletion
{
    public int Id { get; set; }

    /// <summary>O movement_id do movimento apagado.</summary>
    public int BqMovementId { get; set; }

    /// <summary>O bq_repair_trace_id a que o movimento pertencia.</summary>
    public int BqRepairTraceId { get; set; }

    public BqMovementType Type { get; set; }

    public int Quantity { get; set; }

    /// <summary>A data/hora real do movimento apagado (UTC).</summary>
    public DateTime OccurredAtUtc { get; set; }

    public string? MachineCodeFrozen { get; set; }

    /// <summary>O nome de quem apagou, congelado; null quando o pedido chegou sem identidade.</summary>
    public string? DeletedByNameFrozen { get; set; }

    /// <summary>O utilizador de <see cref="DeletedByNameFrozen"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? DeletedByUserId { get; set; }

    public DateTime DeletedAtUtc { get; set; }
}

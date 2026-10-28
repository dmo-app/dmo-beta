namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Read model purpose-specific do Registo: um movimento persistido do
/// trace, tal como o Registo o apresenta — movement_id canónico, token
/// canónico persistido (saida / entrada / entrada_sem_reparacao),
/// quantidade observada registada na totalidade, a discrepância produzida
/// pelo backend (null quando o movimento não tem discrepância), a DATA/HORA
/// REAL do movimento (o occurredAt — o facto humano do html: «Cada
/// movimento mantém o lote, a máquina, a data e o reparador») e o
/// reparador congelado da saída (null nos movimentos que não o definem).
/// A última correção (quem e quando) acompanha o movimento; null = nunca corrigido.
/// </summary>
public sealed record BoquilhasRegistoMovement(
    int MovementId,
    string MovementType,
    int Quantity,
    int? Discrepancy,
    DateTime OccurredAtUtc,
    string? BqRepairerNameFrozen = null,
    string? MachineCodeFrozen = null,
    string? CorrectedByNameFrozen = null,
    DateTime? CorrectedAtUtc = null,
    // Os AN do movimento (Diogo, 28/10/2026); null = sem AN.
    int? AnQuantity = null);

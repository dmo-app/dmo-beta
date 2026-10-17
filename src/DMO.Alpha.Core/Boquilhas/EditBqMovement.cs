namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: corrigir um movimento já gravado (tipo, quantidade e data/hora
/// reais). A data chega em hora local da fábrica; null mantém a data atual.
/// <c>CorrectedBy</c> é o nome de quem corrige; sem identidade grava na mesma.
/// </summary>
public sealed record EditBqMovement(
    int BqRepairTraceId,
    int BqMovementId,
    string MovementType,
    int Quantity,
    DateTime? OccurredAtLocal = null,
    string? CorrectedBy = null);

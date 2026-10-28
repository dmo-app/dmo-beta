namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: corrigir um movimento já gravado (tipo, quantidade e data/hora
/// reais). A data chega em hora local da fábrica; null mantém a data atual;
/// uma data no futuro (para lá da folga de
/// <see cref="BqMovementOccurrence.FutureToleranceMinutes"/> minutos) grava e a
/// página avisa.
/// <c>CorrectedBy</c> é o nome de quem corrige; sem identidade grava na mesma.
/// Os AN (Diogo, 28/10/2026) só mudam com <c>ChangesAn</c>: então
/// <c>AnQuantity</c> é a nova quantidade (null ou 0 = sem AN).
/// </summary>
public sealed record EditBqMovement(
    int BqRepairTraceId,
    int BqMovementId,
    string MovementType,
    int Quantity,
    DateTime? OccurredAtLocal = null,
    string? CorrectedBy = null,
    int? AnQuantity = null,
    bool ChangesAn = false);

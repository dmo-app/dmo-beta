namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado do registo: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do movimento persistido (movement_id canónico).
/// <see cref="OccurredInTheFuture"/> diz que a data real ficou no futuro
/// (para lá da folga de <see cref="BqMovementOccurrence.FutureToleranceMinutes"/>
/// minutos): o movimento gravou e a página avisa.
/// </summary>
public sealed record RegisterBqMovementOutcome(
    RegisterBqMovementResult Result,
    RegisteredBqMovement? Movement = null,
    bool OccurredInTheFuture = false);

/// <summary>
/// Read model purpose-specific do movimento de Boquilhas registado.
/// </summary>
public sealed record RegisteredBqMovement(
    int MovementId,
    int BqRepairTraceId,
    string MovementType,
    int Quantity,
    int? Discrepancy);

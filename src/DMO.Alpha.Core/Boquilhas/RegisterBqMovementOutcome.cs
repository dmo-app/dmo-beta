namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado do registo: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do movimento persistido (movement_id canónico).
/// </summary>
public sealed record RegisterBqMovementOutcome(
    RegisterBqMovementResult Result,
    RegisteredBqMovement? Movement = null);

/// <summary>
/// Read model purpose-specific do movimento de Boquilhas registado.
/// </summary>
public sealed record RegisteredBqMovement(
    int MovementId,
    int BqRepairTraceId,
    string MovementType,
    int Quantity,
    int? Discrepancy);

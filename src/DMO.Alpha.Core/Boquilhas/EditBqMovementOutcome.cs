namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado da correção: o outcome tipado e, em caso de sucesso, o
/// movimento corrigido com a discrepância recalculada.
/// <see cref="OccurredAtShiftedLocal"/> traz a hora local gravada quando a
/// hora indicada caía na mudança de hora e passou para a hora seguinte
/// (a página avisa); null quando a hora foi gravada como veio.
/// </summary>
public sealed record EditBqMovementOutcome(
    EditBqMovementResult Result,
    RegisteredBqMovement? Movement = null,
    DateTime? OccurredAtShiftedLocal = null);

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado da correção: o outcome tipado e, em caso de sucesso, o
/// movimento corrigido com a discrepância recalculada.
/// <see cref="OccurredAtShiftedLocal"/> traz a hora local gravada quando a
/// hora indicada caía na mudança de hora e passou para a hora seguinte
/// (a página avisa); null quando a hora foi gravada como veio.
/// <see cref="OccurredInTheFuture"/> diz que a data corrigida ficou no futuro
/// (para lá da folga de <see cref="BqMovementOccurrence.FutureToleranceMinutes"/>
/// minutos): grava e a página avisa.
/// </summary>
public sealed record EditBqMovementOutcome(
    EditBqMovementResult Result,
    RegisteredBqMovement? Movement = null,
    DateTime? OccurredAtShiftedLocal = null,
    bool OccurredInTheFuture = false);

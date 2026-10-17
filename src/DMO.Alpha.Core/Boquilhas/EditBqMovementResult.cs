namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado tipado da correção de um movimento de Boquilhas.
/// </summary>
public enum EditBqMovementResult
{
    Success,

    /// <summary>O movimento não existe ou não pertence ao registo indicado.</summary>
    MovementNotFound,

    /// <summary>O tipo não é saida, entrada nem entrada_sem_reparacao.</summary>
    UnknownMovementType,

    /// <summary>A quantidade é inferior a 1.</summary>
    InvalidQuantity,

    /// <summary>A data/hora indicada é ilegível (a hora da mudança de hora já não recusa: passa para a hora seguinte).</summary>
    InvalidOccurrenceTime
}

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado tipado da eliminação de um movimento de Boquilhas.
/// </summary>
public enum DeleteBqMovementResult
{
    Success,

    /// <summary>O movimento não existe ou não pertence ao registo indicado.</summary>
    MovementNotFound
}

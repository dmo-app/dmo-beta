namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Apaga um movimento e recalcula as discrepâncias do registo.
/// </summary>
public interface IDeleteBqMovementHandler
{
    Task<DeleteBqMovementResult> HandleAsync(
        DeleteBqMovement command,
        CancellationToken cancellationToken = default);
}

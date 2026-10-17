namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Corrige um movimento gravado e recalcula as discrepâncias do registo.
/// </summary>
public interface IEditBqMovementHandler
{
    Task<EditBqMovementOutcome> HandleAsync(
        EditBqMovement command,
        CancellationToken cancellationToken = default);
}

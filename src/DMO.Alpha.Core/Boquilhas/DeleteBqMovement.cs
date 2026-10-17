namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: apagar um dos movimentos mais recentes de um registo.
/// <c>DeletedBy</c> é o nome de quem apaga; sem identidade apaga na mesma.
/// </summary>
public sealed record DeleteBqMovement(
    int BqRepairTraceId,
    int BqMovementId,
    string? DeletedBy = null)
{
    /// <summary>Só os movimentos mais recentes (data real, depois id) podem ser apagados.</summary>
    public const int MostRecentLimit = 5;
}

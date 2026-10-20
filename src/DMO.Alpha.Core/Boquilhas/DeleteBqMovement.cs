namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: apagar um movimento de um registo, qualquer que seja a data
/// (Diogo, 19/10/2026: a contagem é recalculada pela data real, por isso
/// apagar uma guia antiga não deixa o registo errado).
/// <c>DeletedBy</c> é o nome de quem apaga; sem identidade apaga na mesma.
/// </summary>
public sealed record DeleteBqMovement(
    int BqRepairTraceId,
    int BqMovementId,
    string? DeletedBy = null);

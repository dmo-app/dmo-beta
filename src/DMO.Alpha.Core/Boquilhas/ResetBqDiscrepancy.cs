namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: pôr a zero a discrepância acumulada de um registo (Diogo,
/// 17/10/2026). <c>ResetBy</c> é o nome de quem o faz; sem identidade grava na mesma.
/// </summary>
public sealed record ResetBqDiscrepancy(
    int BqRepairTraceId,
    string? ResetBy = null);

/// <summary>Resultado tipado de pôr a discrepância a zero.</summary>
public enum ResetBqDiscrepancyResult
{
    Success,

    /// <summary>O registo indicado não existe.</summary>
    TraceNotFound
}

/// <summary>
/// Põe a zero a discrepância acumulada: daí em diante só contam os movimentos
/// com data real depois desse momento. Não altera nem apaga movimentos.
/// </summary>
public interface IResetBqDiscrepancyHandler
{
    Task<ResetBqDiscrepancyResult> HandleAsync(
        ResetBqDiscrepancy command,
        CancellationToken cancellationToken = default);
}

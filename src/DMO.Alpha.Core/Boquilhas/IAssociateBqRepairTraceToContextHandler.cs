namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Handler do comando de associação de um bq_repair_trace_id existente ao
/// seu bq_id. A associação não altera JobOn nem BqContext, preserva os
/// movimentos existentes e não controla lifecycle do trace; define o
/// BqContextId e LIMPA a âncora direta tool_id (que passa a ser alcançada
/// via bq_id → tool_id).
/// </summary>
public interface IAssociateBqRepairTraceToContextHandler
{
    Task<AssociateBqRepairTraceToContextOutcome> HandleAsync(
        AssociateBqRepairTraceToContext command,
        CancellationToken cancellationToken = default);
}

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado tipado da criação de um registo de reparação de Boquilhas.
/// </summary>
public enum CreateBqRepairTraceResult
{
    Success,

    /// <summary>O tool_id não resolve a Tool canónica no registo Ferramentas.</summary>
    ToolNotFound,

    /// <summary>
    /// Já existe um trace pré-produção não resolvido (bq_id = null) para
    /// este tool_id: em vez de criar um segundo, o handler devolve o
    /// bq_repair_trace_id existente. Existe no máximo um trace não
    /// resolvido por tool_id.
    /// </summary>
    ReusedExistingUnresolvedTrace
}

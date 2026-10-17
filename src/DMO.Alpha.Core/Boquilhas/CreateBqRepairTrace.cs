namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: criar um registo de reparação de Boquilhas (bq_repair_trace_id)
/// ancorado a um tool_id canónico existente. Input: apenas o tool_id. O
/// trace nasce sem contexto de produção (bq_id = null); nenhum Job On é
/// inferido ou exigido. Existe no máximo um trace pré-produção não
/// resolvido por tool_id: se já existir um (bq_id = null) para este
/// tool_id, o handler reutiliza-o e devolve o bq_repair_trace_id existente
/// em vez de criar um segundo.
/// </summary>
public sealed record CreateBqRepairTrace(string ToolId);

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado da criação: o outcome tipado e, em caso de sucesso (ou de
/// reutilização de um trace não resolvido existente), o read model
/// purpose-specific do trace (bq_repair_trace_id canónico, âncora tool_id,
/// bq_id ainda não resolvido).
/// </summary>
public sealed record CreateBqRepairTraceOutcome(
    CreateBqRepairTraceResult Result,
    CreatedBqRepairTrace? Trace = null);

/// <summary>
/// Read model purpose-specific do trace de reparação criado (ou do trace
/// não resolvido existente reutilizado). O tool_id segue a nulabilidade da
/// âncora da entidade.
/// </summary>
public sealed record CreatedBqRepairTrace(
    int BqRepairTraceId,
    string? ToolId,
    int? BqContextId);

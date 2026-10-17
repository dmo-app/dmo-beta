namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado da associação: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do trace associado (mesma identidade, agora com
/// bq_id resolvido e com a âncora direta tool_id limpa — a Tool alcança-se
/// via bq_id → tool_id).
/// </summary>
public sealed record AssociateBqRepairTraceToContextOutcome(
    AssociateBqRepairTraceToContextResult Result,
    AssociatedBqRepairTrace? Trace = null);

/// <summary>
/// Read model purpose-specific do trace associado ao contexto de produção.
/// <see cref="ToolId"/> reporta o tool_id EFETIVO da Tool canónica — o
/// tool_id alcançado via bq_id → BqContext.ToolId — para o outcome se manter
/// auto-descritivo. A âncora DIRETA persistida no trace é limpa na associação
/// (a coluna ToolId do trace fica null); este valor não reintroduz uma âncora
/// persistida, apenas descreve a Tool já resolvida pela cadeia bq_id.
/// </summary>
public sealed record AssociatedBqRepairTrace(
    int BqRepairTraceId,
    string? ToolId,
    int BqContextId);

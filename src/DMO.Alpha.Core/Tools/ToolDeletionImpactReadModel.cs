namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Read model purpose-specific do AVISO de eliminação de uma Tool: as
/// produções cujos contextos (Cm/Mf/BqContext — strings penduradas,
/// verdade histórica sem FK) referem o tool_id, e as contagens dos
/// registos de consumo que a transação de eliminação reatribui à DUMMY
/// (Pesos, Pegamentos) ou cuja âncora direta limpa (traces não
/// resolvidos). A existência de histórico NÃO remove a opção de eliminar
/// (blueprint L145: é motivo de aviso e confirmação explícita, nunca
/// recusa automática) — a lista cobre a decisão informada do Owner.
/// </summary>
public sealed record ToolDeletionImpactReadModel(
    string ToolId,
    IReadOnlyList<ToolDeletionImpactProduction> Productions,
    int PesoCount,
    int PegamentosCount,
    int UnresolvedTraceCount);

/// <summary>
/// Uma produção afetada: o JobOn cujo contexto (cm_id/mf_id/bq_id)
/// referencia o tool_id da Tool a eliminar. Os contextos de produção NÃO
/// são tocados pela eliminação (verdade histórica pendurada) — aparecem no
/// aviso para a decisão informada.
/// </summary>
public sealed record ToolDeletionImpactProduction(
    int JobOnId,
    string ProductionNumber,
    string Reference,
    string MachineCode);

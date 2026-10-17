namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Serviço de leitura consumer-specific do surface Registo de Boquilhas,
/// resolvido por bq_repair_trace_id canónico. Não é um repositório
/// genérico de Boquilhas nem um rail de produção.
/// </summary>
public interface IBoquilhasRegistoQuery
{
    /// <summary>
    /// Devolve o read model do trace canónico pedido, ou null quando o
    /// bq_repair_trace_id não existe.
    /// </summary>
    Task<BoquilhasRegistoTrace?> FindTraceAsync(int bqRepairTraceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A produção de um bq_id com o trace JÁ ASSOCIADO (um bq_id tem um único
    /// trace; null quando ainda não tem) e a ferramenta do contexto, numa
    /// consulta (mais uma para os movimentos quando há trace). Null quando o
    /// bq_id não existe.
    /// </summary>
    Task<BoquilhasRegistoContext?> FindByContextAsync(int bqContextId, CancellationToken cancellationToken = default);
}

/// <summary>
/// O bq_id aberto no registo: a ferramenta do contexto (Tool null quando não
/// está registada no Ferramentas), a produção e o trace associado, se existe.
/// </summary>
public sealed record BoquilhasRegistoContext(
    int BqContextId,
    string ToolId,
    BoquilhasRegistoProductionContext Production,
    DMO.Alpha.Core.Tools.ToolLookupResult? Tool,
    int? ToolAccountedQuantity,
    BoquilhasRegistoTrace? Trace);

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: associar um bq_repair_trace_id existente (ainda não resolvido)
/// ao seu bq_id (contexto BQ de produção existente). A associação preserva
/// o bq_repair_trace_id e todos os movimentos existentes e define o
/// BqContextId; em contrapartida LIMPA a âncora direta temporária
/// (tool_id = null): após a associação a Tool passa a ser alcançada via
/// bq_id → tool_id. Não recria nem copia o trace; não altera JobOn nem
/// BqContext; não arranca, para, fecha ou reabre o trace. A escolha de
/// QUAL trace não resolvido associar pertence ao caller — este handler não
/// escolhe automaticamente entre candidatos.
/// </summary>
public sealed record AssociateBqRepairTraceToContext(
    int BqRepairTraceId,
    int BqContextId);

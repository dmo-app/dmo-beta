using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Registo de reparação de Boquilhas (repair trace) para um contexto BQ de
/// produção. A identidade canónica é o bq_repair_trace_id, emitido pelo
/// backend: um trace agrupa todos os ciclos de movimento de um bq_id e não
/// é criado por viagem de reparação.
/// O tool_id é uma ÂNCORA DIRETA TEMPORÁRIA usada apenas na fase
/// pré-produção (bq_id ainda não resolvido) para correlacionar o trace com
/// a Tool física BQ. No momento da associação a bq_id esta âncora é LIMPA
/// (ToolId = null): a partir daí a Tool é alcançada via bq_id → tool_id
/// (BqContext.ToolId), nunca pela âncora direta. Existe no máximo um trace
/// pré-produção não resolvido por tool_id (a âncora direta é única enquanto
/// BqContextId é null); traces históricos já associados a bq_id mais
/// antigos têm a âncora limpa e coexistem livremente. A associação não
/// recria, substitui nem reinicia o trace. Não existe estado open/closed
/// nem lifecycle — Job On não arranca nem para um trace.
/// Entidade mínima de identidade e âncoras; os movimentos pertencem a um
/// slice futuro.
/// </summary>
public sealed class BqRepairTrace
{
    public int Id { get; set; }

    /// <summary>
    /// Âncora direta temporária à Tool física BQ. Preenchida na criação
    /// pré-produção e LIMPA (null) na associação a bq_id; depois da
    /// associação a Tool alcança-se via bq_id → BqContext.ToolId.
    /// Na eliminação da Tool, os traces não resolvidos ficam com esta
    /// âncora a NULL (o índice único parcial aceita NULLs) e os factos
    /// congelados <see cref="ToolReferenceFrozen"/>/<see cref="ToolLotFrozen"/>
    /// preservam a verdade histórica do trace.
    /// </summary>
    public string? ToolId { get; set; }

    public Tool? Tool { get; set; }

    /// <summary>
    /// bq_id — o contexto BQ (BqContext) usado num jobon_id. Null enquanto
    /// o trace não tem contexto de produção (pré-produção, não resolvido).
    /// </summary>
    public int? BqContextId { get; set; }

    public BqContext? BqContext { get; set; }

    /// <summary>
    /// Referência canónica da Tool BQ congelada à criação do trace
    /// (evidência histórica): o trace mantém os factos humanos da Tool que
    /// o originou mesmo depois de a Tool ser editada ou eliminada (na
    /// eliminação a âncora direta é limpa/reatribuída, os factos
    /// congelados ficam). Backfill no handler de eliminação para traces
    /// anteriores a esta coluna.
    /// </summary>
    public string? ToolReferenceFrozen { get; set; }

    /// <summary>Lote da Tool BQ congelado à criação do trace (evidência histórica; ver <see cref="ToolReferenceFrozen"/>).</summary>
    public string? ToolLotFrozen { get; set; }

    /// <summary>
    /// A discrepância posta a zero (Diogo, 17/10/2026: começar a registar a
    /// meio das produções): a discrepância acumulada do registo só conta os
    /// movimentos com data real depois deste momento (UTC). Os movimentos e o
    /// saldo de cada linha ficam como estão. Null = nunca posta a zero.
    /// </summary>
    public DateTime? DiscrepancyResetAtUtc { get; set; }

    /// <summary>Quem pôs a discrepância a zero, com o nome congelado; null sem identidade.</summary>
    public string? DiscrepancyResetByNameFrozen { get; set; }

    /// <summary>O utilizador de <see cref="DiscrepancyResetByNameFrozen"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? DiscrepancyResetByUserId { get; set; }
}

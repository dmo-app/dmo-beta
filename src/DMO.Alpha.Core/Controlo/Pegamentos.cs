using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Controlo de Pegamentos production-scoped — o registo operacional durável
/// identificado pelo <c>pegamentos_id</c> (emitido pelo backend), que vive
/// SOB o controlo de uma produção (a LEI da app: «a partir do jobon nasce o
/// controlo_id e com controlo_id ganhas o contexto todo para pegamentos e
/// peso»; spec Controlo Create — Pegamentos L12: «When a production context
/// exists, Pegamentos operates under the existing Controlo context»).
/// O registo ancora as Tools canónicas herdadas do contexto Job On
/// (cm_id → CM tool_id, bq_id → BQ tool_id, mf_id → MF tool_id — spec
/// L146-148) no momento da criação, como evidência histórica da combinação
/// CM+BQ+MF medida, e congela a tolerância de corredor efetiva consumida
/// (decisão do Owner: «CONGELADA no registo no momento da pesagem (a
/// evidência histórica guarda o valor usado)»). A ausência é um estado
/// válido (spec/PDF §10 L493-494): um controlo sem nenhuma medição ainda
/// não tem registo de Pegamentos. A cardinalidade da spec (L22-28) é
/// «normalmente um registo por produção» e NÃO uma restrição absoluta
/// «um só registo» — por isso a coluna ControloId não é única. Uma folha é
/// de UMA combinação CM+BQ+MF: depois de trocar a CM, a BQ ou o MF a meio
/// da produção, a primeira leitura começa outro pegamentos_id com as
/// ferramentas novas e a folha anterior fica como estava (
/// 18/10/2026). As leituras pertencem ao registo e são factos
/// históricos append-only, como no Peso.
/// </summary>
public sealed class Pegamentos
{
    public int Id { get; set; }

    /// <summary>
    /// controlo_id da produção a que esta folha de Pegamentos pertence —
    /// a âncora canónica (a folha vive sob o controlo; o contexto da
    /// produção resolve-se pela relação controlo → jobon → contextos).
    /// Não é único: a cardinalidade normal «um por produção» não é
    /// restrição de domínio (spec L26-28). Null nos Pegamentos sem Job On
    /// (as ferramentas escolhidas à mão); associar a um Job On mais tarde
    /// preenche-o.
    /// </summary>
    public int? ControloId { get; set; }

    public DMO.Alpha.Core.Controlo.Controlo? Controlo { get; set; }

    /// <summary>
    /// tool_id canónica da CM usada na combinação, herdada do contexto Job
    /// On no momento da criação do registo (null quando o contexto não a
    /// tem). O operador nunca seleciona ferramentas na folha (spec L92).
    /// </summary>
    public string? CmToolId { get; set; }

    public Tool? CmTool { get; set; }

    /// <summary>
    /// tool_id canónica da BQ usada na combinação, herdada do contexto Job
    /// On no momento da criação do registo (null quando o contexto não a
    /// tem).
    /// </summary>
    public string? BqToolId { get; set; }

    public Tool? BqTool { get; set; }

    /// <summary>
    /// tool_id canónica da MF usada na combinação, herdada do contexto Job
    /// On no momento da criação do registo. Null é o estado real comum
    /// (o Job On J10 da base real não tem MF — «MF não aplicável»).
    /// </summary>
    public string? MfToolId { get; set; }

    public Tool? MfTool { get; set; }

    /// <summary>
    /// Tolerância do corredor (mm) efetiva no momento da criação do registo
    /// — a configuração configurada no Controlo quando existia (decisão do
    /// Owner: default 0,20, CONFIGURÁVEL, nunca locked) e, na sua falta, o
    /// default canónico ±0,20 do Owner. CONGELADA como evidência histórica:
    /// uma alteração posterior da configuração não reescreve as avaliações
    /// das medições deste registo (CONFIGURATION.md L47-51: «A later
    /// configuration change must not rewrite historical calculation truth
    /// already consumed by a completed operational record»). Todas as
    /// linhas do registo são avaliadas com este valor.
    /// </summary>
    public decimal ToleranceMm { get; set; }

    /// <summary>
    /// Referência canónica da CM congelada à criação do registo (evidência
    /// histórica): as âncoras humanas da combinação CM+BQ+MF vivem no
    /// REGISTO, não nas medições (a PegamentosMeasurement congela o
    /// nominal por linha; a referência/lote pertencem à combinação). A
    /// app mostra FATOS HUMANOS no histórico mesmo depois de as Tools
    /// serem editadas ou eliminadas (a reatribuição ao DUMMY preserva os
    /// factos congelados). Backfill no handler de eliminação para
    /// registos anteriores a estas colunas. Null quando o componente não
    /// aplicava (CmToolId null).
    /// </summary>
    public string? CmToolReferenceFrozen { get; set; }

    /// <summary>Lote da CM congelado à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? CmToolLotFrozen { get; set; }

    /// <summary>Referência da BQ congelada à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? BqToolReferenceFrozen { get; set; }

    /// <summary>Lote da BQ congelado à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? BqToolLotFrozen { get; set; }

    /// <summary>Referência da MF congelada à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? MfToolReferenceFrozen { get; set; }

    /// <summary>Lote da MF congelado à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? MfToolLotFrozen { get; set; }

    /// <summary>
    /// Quando o registo foi gravado (UTC, pelo backend). Null nos registos
    /// anteriores a 17/10/2026: a data não foi guardada e não se inventa.
    /// A Revisão do Controlo usa-a para pôr o controlo no dia do calendário.
    /// </summary>
    public DateTime? RegistadoEmUtc { get; set; }

    /// <summary>Quem gravou o registo (o nome da sessão, congelado); null quando não se sabe.</summary>
    public string? RegistadoPor { get; set; }

    /// <summary>O utilizador de <see cref="RegistadoPor"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? RegistadoPorUserId { get; set; }

    /// <summary>
    /// A última correção de uma leitura desta folha (mudar ou apagar), em
    /// UTC; null sem correções ou antes de 19/10/2026. A Revisão avisa
    /// «Corrigido depois da aprovação» quando é posterior à decisão (Diogo,
    /// 19/10/2026, C9); a aprovação mantém-se.
    /// </summary>
    public DateTime? CorrigidoEmUtc { get; set; }

    /// <summary>Leituras registadas nesta folha (factos históricos append-only).</summary>
    public ICollection<PegamentosMeasurement> Measurements { get; set; } = new List<PegamentosMeasurement>();
}

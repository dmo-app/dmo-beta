using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Controlo — o registo operacional durável identificado pelo
/// <c>controlo_id</c> (emitido pelo backend), associado a UMA produção
/// (Job On) pela relação <c>JobOnId</c> com índice único (spec Job On L33:
/// «When a Job On is created, the associated controlo_id is also created»).
/// Neste corte a entidade é a identidade e a relação, NADA MAIS: nenhum
/// facto CM/BQ/MF/PU/CS (edição é corte futuro), nenhum timestamp, nenhum
/// estado/revisão, nenhuma cópia do contexto de produção — o contexto
/// resolve-se pela relação com a query canónica
/// <see cref="IProductionContextQuery"/>. O registo existe desde a criação
/// do Job On, antes de o Peso ou outra função de Controlo estar concluída
/// (spec Controlo): o ensure é get-or-create idempotente por JobOnId,
/// nunca um ato de criação separado do utilizador.
/// </summary>
public sealed class Controlo
{
    public int Id { get; set; }

    /// <summary>
    /// jobon_id da produção a que este controlo_id está associado — único
    /// por construção (índice único na coluna): uma produção tem um
    /// controlo_id, e abri-lo duas vezes devolve o MESMO controlo_id.
    /// </summary>
    public int JobOnId { get; set; }

    /// <summary>
    /// Navegação para a produção associada (forma dos Cm/Mf/BqContexts),
    /// usada pela escrita atómica na criação do Job On. O Job On NÃO ganha
    /// navegação nem coluna de Controlo (spec Job On L31: Job On não se
    /// torna owner dos registos de Controlo por os consumir).
    /// </summary>
    public JobOn JobOn { get; set; } = null!;

    /// <summary>
    /// O ESTADO DE APROVAÇÃO (o corte D — a GATE 4 universal: o registo
    /// histórico de que a informação existiu e foi VISTA; o APROVAR é o
    /// selo do chefe por cima do visto): o operador que aprovou o controlo
    /// desta produção. Null = ainda não aprovado — e a regra absoluta: NÃO
    /// APROVADO NUNCA BLOQUEIA NEM NEGA CONTINUIDADE (o Owner: «a app de
    /// forma alguma pode bloquear»).
    /// </summary>
    public string? ApprovedByOperatorId { get; set; }

    /// <summary>O momento da aprovação (UTC, gravado pelo backend).</summary>
    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>
    /// O «Não aprovar» da Revisão (17/10/2026): quem o marcou. Tal como a
    /// aprovação, é só registo e nunca bloqueia; aprovar depois limpa-o.
    /// </summary>
    public string? NotApprovedByOperatorId { get; set; }

    /// <summary>O momento do «Não aprovar» (UTC, gravado pelo backend).</summary>
    public DateTime? NotApprovedAtUtc { get; set; }

    /// <summary>A nota do chefe na última decisão (obrigatória ao não aprovar).</summary>
    public string? ReviewNote { get; set; }
}

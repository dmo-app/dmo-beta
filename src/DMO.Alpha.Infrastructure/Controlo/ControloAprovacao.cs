using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O estado da aprovação de cada controlo, num só sítio (sweep de 19/10/2026,
/// C-1; o especialista, R28). Só o Peso se aprova (Diogo, 19/10/2026:
/// «remover aprovaçao do spegamentos, manter apenas no peso»), por isso o
/// estado do controlo é o da decisão do Peso, calculado ao ler:
/// <list type="bullet">
/// <item>não aprovado, se o Peso tem «Não aprovar»;</item>
/// <item>aprovado, se o Peso tem «Aprovar» e não chegou nenhum Peso novo
/// depois (a CM ou a BQ trocou: o registo novo tem aprovação própria, Diogo,
/// 19/10/2026, C7). Leituras novas ou corrigidas num Peso já aprovado não
/// mudam a aprovação: a Revisão só avisa (C9);</item>
/// <item>por aprovar, nos outros casos.</item>
/// </list>
/// Um controlo sem decisões por parte (anterior a elas, ou com o resumo
/// escrito diretamente) lê o resumo gravado nas colunas do controlo. As
/// decisões antigas dos Pegamentos ficam na base e não contam. A Revisão, o
/// Histórico, o Resumo, os PDFs e o aviso «mudou depois da aprovação» leem
/// daqui; as colunas guardam o resumo da última decisão.
/// </summary>
internal static class ControloAprovacao
{
    /// <summary>
    /// Sem pesoId, resolve o estado corrente do controlo. Com pesoId, resolve
    /// a decisão aplicável só à folha pedida, sem invalidar uma folha antiga
    /// por causa de outra mais recente. Nunca cruza o Peso com outro controlo.
    /// </summary>
    public static IQueryable<ControloAprovacaoEstado> Estados(DmoDbContext db, int? pesoId = null) =>
        (from c in db.Controlos.AsNoTracking()
         where pesoId == null || db.Pesos.Any(p => p.Id == pesoId && p.ControloId == c.Id)
         from peso in db.ControloDecisoes
             .Where(d => d.ControloId == c.Id && d.Parte == ControloParte.Peso)
             .DefaultIfEmpty()
         select new
         {
             c,
             peso,
             SemDecisoes = !db.ControloDecisoes.Any(d => d.ControloId == c.Id),
             // Um Peso novo depois da decisão (a CM ou a BQ trocou): tem
             // aprovação própria (Diogo, 19/10/2026, C7).
             Aprovado = peso != null && peso.Aprovado
                 && !db.Pesos.Any(p => p.ControloId == c.Id && (pesoId == null || p.Id == pesoId)
                     && p.RegistadoEmUtc > peso.EmUtc),
             NaoAprovado = peso != null && !peso.Aprovado
         })
        .Select(x => new ControloAprovacaoEstado
        {
            ControloId = x.c.Id,
            JobOnId = x.c.JobOnId,
            ApprovedByOperatorId = x.SemDecisoes ? x.c.ApprovedByOperatorId : x.Aprovado ? x.peso!.OperatorId : null,
            ApprovedAtUtc = x.SemDecisoes ? x.c.ApprovedAtUtc : x.Aprovado ? x.peso!.EmUtc : null,
            NotApprovedByOperatorId = x.SemDecisoes ? x.c.NotApprovedByOperatorId : x.NaoAprovado ? x.peso!.OperatorId : null,
            NotApprovedAtUtc = x.SemDecisoes ? x.c.NotApprovedAtUtc : x.NaoAprovado ? x.peso!.EmUtc : null,
            ReviewNote = x.SemDecisoes ? x.c.ReviewNote : x.Aprovado || x.NaoAprovado ? x.peso!.Nota : null
        });
}

/// <summary>
/// O estado da aprovação de um controlo, com a forma das colunas do controlo:
/// aprovado (quem e quando), não aprovado (quem e quando) ou nenhum dos dois
/// (por aprovar), e a nota.
/// </summary>
internal sealed class ControloAprovacaoEstado
{
    public int ControloId { get; init; }
    public int JobOnId { get; init; }
    public string? ApprovedByOperatorId { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public string? NotApprovedByOperatorId { get; init; }
    public DateTime? NotApprovedAtUtc { get; init; }
    public string? ReviewNote { get; init; }
}

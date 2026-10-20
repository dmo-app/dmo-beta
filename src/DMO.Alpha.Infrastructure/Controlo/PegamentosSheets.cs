using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A CM, a BQ e o MF de uma produção num momento (os tool_id do contexto do
/// Job On; null no componente que a produção não tem). Uma folha de
/// Pegamentos é de uma só combinação: as ferramentas vivem no registo, não
/// nas leituras.
/// </summary>
internal sealed record PegamentosCombination(string? CmToolId, string? BqToolId, string? MfToolId)
{
    internal string? ToolOf(PegamentosComponent component) => component switch
    {
        PegamentosComponent.Cm => CmToolId,
        PegamentosComponent.Bq => BqToolId,
        _ => MfToolId
    };

    /// <summary>True quando a folha tem exatamente estas ferramentas (um componente em falta só iguala outro em falta).</summary>
    internal bool Matches(string? cmToolId, string? bqToolId, string? mfToolId) =>
        CmToolId == cmToolId && BqToolId == bqToolId && MfToolId == mfToolId;
}

/// <summary>
/// As folhas de Pegamentos de uma produção por combinação de ferramentas
/// (18/10/2026): depois de trocar a CM, a BQ ou o MF a meio da
/// produção, a leitura seguinte vai para a folha dessa combinação — a que já
/// existia (voltar a uma ferramenta que a produção já usou retoma a folha
/// dela, como o contexto) ou uma nova. Usado pelo registo das leituras, pela
/// associação dos Pegamentos sem Job On e pela leitura da folha.
/// </summary>
internal static class PegamentosSheets
{
    /// <summary>
    /// A combinação que a produção do controlo tem agora (controlo → Job On →
    /// contextos, numa consulta); null quando o controlo não existe.
    /// </summary>
    internal static async Task<PegamentosCombination?> CurrentCombinationAsync(
        DmoDbContext db,
        int controloId,
        CancellationToken cancellationToken)
    {
        var current = await db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => new
            {
                CmToolId = db.CmContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => (string?)x.ToolId).FirstOrDefault(),
                BqToolId = db.BqContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => (string?)x.ToolId).FirstOrDefault(),
                MfToolId = db.MfContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => (string?)x.ToolId).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return current is null ? null : new PegamentosCombination(current.CmToolId, current.BqToolId, current.MfToolId);
    }

    /// <summary>
    /// As folhas do controlo com estas ferramentas. Um componente em falta
    /// (o MF do J10 real) só encontra a folha que também não o tem: a
    /// comparação com um valor null é «IS NULL» no SQL (sem
    /// UseRelationalNulls), como no C#.
    /// </summary>
    internal static IQueryable<DMO.Alpha.Core.Controlo.Pegamentos> OfCombination(
        this IQueryable<DMO.Alpha.Core.Controlo.Pegamentos> source,
        int controloId,
        PegamentosCombination combination)
    {
        var (cm, bq, mf) = (combination.CmToolId, combination.BqToolId, combination.MfToolId);
        return source.Where(p => p.ControloId == controloId
            && p.CmToolId == cm
            && p.BqToolId == bq
            && p.MfToolId == mf);
    }
}

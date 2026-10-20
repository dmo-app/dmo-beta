using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A CM e a BQ de uma produção num momento (os tool_id do contexto do Job
/// On; null no que a produção não tem). O Peso é de uma só combinação: o
/// MF não entra, porque o cálculo (capacidade + marisa − punção) só usa a CM
/// e a BQ (resposta R16-5 do especialista, 18/10/2026).
/// </summary>
internal sealed record PesoCombination(string? CmToolId, string? BqToolId)
{
    /// <summary>
    /// True quando o Peso tem estas ferramentas. Um componente que a produção
    /// não tem (o Job On ficou sem BQ) não afasta o Peso: não há outro Peso
    /// para criar, e o que a produção tinha continua a ser o dela.
    /// </summary>
    internal bool Matches(string cmToolId, string bqToolId) =>
        (CmToolId is null || CmToolId == cmToolId) && (BqToolId is null || BqToolId == bqToolId);
}

/// <summary>
/// Os Pesos de uma produção por combinação de CM e BQ (18/10/2026, regra do
/// Diogo: «o cm_id é snapshot de um tool_id naquele momento»): depois de
/// trocar a CM ou a BQ a meio da produção, o Peso das ferramentas novas é
/// outro; voltar a uma combinação que a produção já usou retoma o Peso dela,
/// como o contexto (respostas R15, R16-2 e R16-5 do especialista). Usado pela
/// leitura do Peso, pelo registo das leituras e pela Revisão.
/// </summary>
internal static class PesoCombinations
{
    /// <summary>
    /// A combinação que a produção do controlo tem agora (controlo → Job On
    /// → contextos, numa consulta); null quando o controlo não existe.
    /// </summary>
    internal static async Task<PesoCombination?> CurrentCombinationAsync(
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
                BqToolId = db.BqContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => (string?)x.ToolId).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return current is null ? null : new PesoCombination(current.CmToolId, current.BqToolId);
    }

    /// <summary>
    /// O peso_id do Peso atual do controlo — o da combinação que a produção
    /// tem agora — ou null quando ainda não existe. Se dois Pesos ficaram com
    /// a mesma combinação (as ferramentas apagadas passam a REMOVED), o mais
    /// antigo, como nas folhas de Pegamentos.
    /// </summary>
    internal static async Task<int?> CurrentPesoIdAsync(
        DmoDbContext db,
        int controloId,
        CancellationToken cancellationToken)
    {
        var current = await CurrentCombinationAsync(db, controloId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        return await db.Pesos
            .AsNoTracking()
            .OfCombination(controloId, current)
            .OrderBy(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Os Pesos do controlo com estas ferramentas (ver <see cref="PesoCombination.Matches"/>).</summary>
    internal static IQueryable<DMO.Alpha.Core.Controlo.Peso> OfCombination(
        this IQueryable<DMO.Alpha.Core.Controlo.Peso> source,
        int controloId,
        PesoCombination combination)
    {
        var (cm, bq) = (combination.CmToolId, combination.BqToolId);
        return source.Where(p => p.ControloId == controloId
            && (cm == null || p.CmToolId == cm)
            && (bq == null || p.BqToolId == bq));
    }
}

using System.Linq.Expressions;
using DMO.Alpha.Core.Production;
using Microsoft.EntityFrameworkCore.Query;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A regra única de «anterior» no Controlo (sweep de 19/10/2026, C-2). A
/// «Produção anterior» do Peso, a comparação com o controlo anterior e o
/// «Controlo anterior» da Revisão escolhem pela mesma ordem: a Revisão
/// seguia a ordem de registo e podia mostrar como anterior uma produção que
/// começou depois. As anteriores são as que começaram antes da atual (no
/// mesmo dia, as registadas antes); sem início na atual, as registadas antes
/// dela. A comparação e a Revisão contam também só as máquinas da ficha da CM
/// (o especialista, R29).
/// <para>
/// Cada condição recebe onde está o Job On de cada linha (<c>j =&gt; j</c>, ou
/// <c>x =&gt; x.j</c> numa junção), para ficar na mesma consulta SQL.
/// </para>
/// </summary>
internal static class ProducaoAnterior
{
    /// <summary>Só as produções anteriores à atual.</summary>
    public static IQueryable<T> SoAnteriores<T>(
        this IQueryable<T> query,
        Expression<Func<T, JobOn>> jobOn,
        int atualId,
        DateOnly? atualInicio)
    {
        Expression<Func<JobOn, bool>> anterior = atualInicio is DateOnly inicio
            ? j => j.InicioEm != null && (j.InicioEm < inicio || (j.InicioEm == inicio && j.Id < atualId))
            : j => j.Id < atualId;
        return query.Where(Sobre(jobOn, anterior));
    }

    /// <summary>
    /// Só as produções das máquinas da ficha da CM (o Owner, 12/10/2026: «se
    /// a tool aceita B1 e C3 e o último for C3 pode ser comparado»). Sem CM,
    /// ou com a ficha sem máquinas, toda a referência.
    /// </summary>
    public static IQueryable<T> SoMaquinasDaCm<T>(
        this IQueryable<T> query,
        Expression<Func<T, JobOn>> jobOn,
        List<int> maquinasDaCm) =>
        maquinasDaCm.Count == 0
            ? query
            : query.Where(Sobre<T, bool>(jobOn, j => maquinasDaCm.Contains(j.MachineId)));

    /// <summary>
    /// A mais recente primeiro: pelo início, depois pela ordem de registo. No
    /// PostgreSQL os null vêm primeiro num DESC: as sem início vão para o fim
    /// de propósito.
    /// </summary>
    public static IOrderedQueryable<T> MaisRecentePrimeiro<T>(
        this IQueryable<T> query,
        Expression<Func<T, JobOn>> jobOn) =>
        query.OrderBy(Sobre<T, bool>(jobOn, j => j.InicioEm == null))
            .ThenByDescending(Sobre<T, DateOnly?>(jobOn, j => j.InicioEm))
            .ThenByDescending(Sobre<T, int>(jobOn, j => j.Id));

    private static Expression<Func<T, TResult>> Sobre<T, TResult>(
        Expression<Func<T, JobOn>> jobOn,
        Expression<Func<JobOn, TResult>> regra) =>
        Expression.Lambda<Func<T, TResult>>(
            ReplacingExpressionVisitor.Replace(regra.Parameters[0], jobOn.Body, regra.Body),
            jobOn.Parameters);
}

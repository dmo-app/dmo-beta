using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// As alterações registadas de cada Job On, para «O que mudou» na ficha, para
/// a marca nas linhas de «Produções do dia» e para o aviso nos módulos. Só leitura.
/// </summary>
public sealed class JobOnChangeQuery
{
    private readonly DmoDbContext _db;

    public JobOnChangeQuery(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>Todas as alterações de uma produção, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<JobOnChangeView>> ForJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.JobOnChanges
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Where(JobOnChangeFields.Counts)
            .OrderByDescending(c => c.ChangedAtUtc)
            .ThenBy(c => c.Id)
            .Select(c => new { c.JobOnId, c.Field, c.OldValueFrozen, c.NewValueFrozen, c.ChangedByNameFrozen, c.ChangedAtUtc })
            .ToListAsync(cancellationToken);
        return rows
            .Select(c => new JobOnChangeView(
                c.JobOnId,
                c.Field,
                JobOnChangeFields.Describe(c.Field, c.OldValueFrozen, c.NewValueFrozen),
                c.ChangedByNameFrozen,
                c.ChangedAtUtc))
            .ToList();
    }

    // Os avisos dos módulos: cada um é UMA consulta (as alterações vigiadas
    // do Job On do registo, depois da hora do registo), para não pesar nas
    // páginas que já contam as consultas.

    /// <summary>O aviso do Peso: o que mudou no Job On depois de o Peso ser gravado.</summary>
    public Task<JobOnChangeNotice?> ForPesoAsync(int pesoId, CancellationToken cancellationToken = default) =>
        NoticeAsync(JobOnChangeFields.PesoWatches, c => _db.Pesos.Any(p =>
            p.Id == pesoId
            && p.Controlo!.JobOnId == c.JobOnId
            && p.RegistadoEmUtc < c.ChangedAtUtc), cancellationToken);

    /// <summary>
    /// O aviso de uma folha de Pegamentos: o que mudou no Job On depois de
    /// ESTA folha ser gravada. A folha que nasceu de uma troca não lista a
    /// troca que a criou (antes era a data da primeira folha do controlo).
    /// </summary>
    public Task<JobOnChangeNotice?> ForPegamentosSheetAsync(int pegamentosId, CancellationToken cancellationToken = default) =>
        NoticeAsync(JobOnChangeFields.PegamentosWatches, c => _db.Pegamentos.Any(g =>
            g.Id == pegamentosId
            && g.Controlo!.JobOnId == c.JobOnId
            && g.RegistadoEmUtc < c.ChangedAtUtc), cancellationToken);

    /// <summary>
    /// O aviso do Controlo aprovado: o que mudou depois da aprovação (a
    /// aprovação mantém-se). A aprovação é a do estado do controlo
    /// (ControloAprovacao): por aprovar, não há aviso.
    /// </summary>
    public Task<JobOnChangeNotice?> ForControloApprovalAsync(int controloId, CancellationToken cancellationToken = default)
    {
        var estados = ControloAprovacao.Estados(_db);
        return NoticeAsync(JobOnChangeFields.ControloWatches, c => estados.Any(k =>
            k.ControloId == controloId
            && k.JobOnId == c.JobOnId
            && k.ApprovedAtUtc < c.ChangedAtUtc), cancellationToken);
    }

    /// <summary>
    /// O aviso de um registo das Boquilhas (pelo bq_id): o que mudou depois
    /// do primeiro movimento gravado. Sem movimentos ainda não há registo feito.
    /// </summary>
    public Task<JobOnChangeNotice?> ForBqContextAsync(int bqContextId, CancellationToken cancellationToken = default) =>
        NoticeAsync(JobOnChangeFields.BoquilhasWatches, c => _db.BqContexts.Any(b =>
            b.Id == bqContextId
            && b.JobOnId == c.JobOnId
            && _db.BqMovements.Where(m => m.Trace.BqContextId == b.Id).Min(m => (DateTime?)m.CreatedAtUtc) < c.ChangedAtUtc), cancellationToken);

    private async Task<JobOnChangeNotice?> NoticeAsync(
        IReadOnlyList<string> watches,
        System.Linq.Expressions.Expression<Func<JobOnChange, bool>> afterTheRecord,
        CancellationToken cancellationToken)
    {
        var fields = watches.ToArray();
        var rows = await _db.JobOnChanges
            .AsNoTracking()
            .Where(c => fields.Contains(c.Field))
            .Where(JobOnChangeFields.Counts)
            .Where(afterTheRecord)
            .OrderByDescending(c => c.ChangedAtUtc)
            .ThenBy(c => c.Id)
            .Select(c => new { c.JobOnId, c.Field, c.OldValueFrozen, c.NewValueFrozen, c.ChangedByNameFrozen, c.ChangedAtUtc })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        return new JobOnChangeNotice(
            rows[0].JobOnId,
            rows.Select(c => new JobOnChangeView(
                    c.JobOnId,
                    c.Field,
                    JobOnChangeFields.Describe(c.Field, c.OldValueFrozen, c.NewValueFrozen),
                    c.ChangedByNameFrozen,
                    c.ChangedAtUtc))
                .ToList());
    }

    /// <summary>
    /// As alterações feitas depois do início de cada uma das produções
    /// indicadas, da mais recente para a mais antiga (só as que têm alguma):
    /// a linha da máquina em «Produções do dia» mostra qual foi a alteração.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<JobOnChangeView>>> AfterStartAsync(
        IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        if (jobOnIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<JobOnChangeView>>();
        }

        var rows = await _db.JobOnChanges
            .AsNoTracking()
            .Where(c => jobOnIds.Contains(c.JobOnId))
            .Where(JobOnChangeFields.Counts)
            .OrderByDescending(c => c.ChangedAtUtc)
            .ThenBy(c => c.Id)
            .Select(c => new { c.JobOnId, c.Field, c.OldValueFrozen, c.NewValueFrozen, c.ChangedByNameFrozen, c.ChangedAtUtc, c.JobOn!.InicioEm })
            .ToListAsync(cancellationToken);
        return rows
            .Where(c => JobOnChange.AfterStart(c.ChangedAtUtc, c.InicioEm))
            .GroupBy(c => c.JobOnId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<JobOnChangeView>)g
                    .Select(c => new JobOnChangeView(
                        c.JobOnId,
                        c.Field,
                        JobOnChangeFields.Describe(c.Field, c.OldValueFrozen, c.NewValueFrozen),
                        c.ChangedByNameFrozen,
                        c.ChangedAtUtc)
                    {
                        OldValue = c.OldValueFrozen,
                        NewValue = c.NewValueFrozen
                    })
                    .ToList());
    }
}

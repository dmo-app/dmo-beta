using DMO.Alpha.Core.Production;
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

    /// <summary>O aviso dos Pegamentos: o que mudou depois do primeiro registo de Pegamentos do controlo.</summary>
    public Task<JobOnChangeNotice?> ForPegamentosAsync(int controloId, CancellationToken cancellationToken = default) =>
        NoticeAsync(JobOnChangeFields.PegamentosWatches, c => _db.Controlos.Any(k =>
            k.Id == controloId
            && k.JobOnId == c.JobOnId
            && _db.Pegamentos.Where(g => g.ControloId == k.Id).Min(g => g.RegistadoEmUtc) < c.ChangedAtUtc), cancellationToken);

    /// <summary>O aviso do Controlo aprovado: o que mudou depois da aprovação (a aprovação mantém-se).</summary>
    public Task<JobOnChangeNotice?> ForControloApprovalAsync(int controloId, CancellationToken cancellationToken = default) =>
        NoticeAsync(JobOnChangeFields.ControloWatches, c => _db.Controlos.Any(k =>
            k.Id == controloId
            && k.JobOnId == c.JobOnId
            && k.ApprovedAtUtc < c.ChangedAtUtc), cancellationToken);

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
    /// Quantas alterações feitas depois do início tem cada uma das produções
    /// indicadas (só as que têm alguma), como o histórico da ficha.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, int>> CountsAsync(
        IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        if (jobOnIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var rows = await _db.JobOnChanges
            .AsNoTracking()
            .Where(c => jobOnIds.Contains(c.JobOnId))
            .Select(c => new { c.JobOnId, c.ChangedAtUtc, c.JobOn!.InicioEm })
            .ToListAsync(cancellationToken);
        return rows
            .Where(c => JobOnChange.AfterStart(c.ChangedAtUtc, c.InicioEm))
            .GroupBy(c => c.JobOnId)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}

using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação purpose-specific de <see cref="IJobOnListQuery"/>: a
/// consulta dos Job Ons existentes com o contexto de produção estabelecido.
/// Usa a mesma projeção direta sobre as tabelas reais que a
/// <see cref="ProductionContextQuery"/> serve por jobon_id (join à
/// máquina, left joins aos contextos aplicáveis); não carrega entidades
/// completas nem introduz um aggregate genérico. Ordenação determinística
/// por jobon_id decrescente (o mais recente primeiro).
/// </summary>
public sealed class JobOnListQuery : IJobOnListQuery
{
    private readonly DmoDbContext _db;

    public JobOnListQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductionContextReadModel>> ListAsync(CancellationToken cancellationToken = default) =>
        await Project(_db.JobOns.AsNoTracking()).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(
        string reference,
        string? machineCode,
        CancellationToken cancellationToken = default)
    {
        var referencia = reference.Trim();
        var jobs = _db.JobOns.AsNoTracking().Where(j => j.Reference == referencia);
        if (!string.IsNullOrWhiteSpace(machineCode))
        {
            var maquina = machineCode.Trim();
            jobs = jobs.Where(j => _db.Machines.Any(m => m.Id == j.MachineId && m.Code == maquina));
        }

        return await Project(jobs)
            .Take(IProductionPickerQuery.MaxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(
        DateOnly de,
        DateOnly ate,
        CancellationToken cancellationToken = default) =>
        await Project(_db.JobOns.AsNoTracking()
                .Where(j => j.InicioEm != null && j.InicioEm <= ate && (j.FimEm == null || j.FimEm >= de)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(
        DateOnly dia,
        CancellationToken cancellationToken = default)
    {
        // O início mais cedo depois do dia, por máquina; a base devolve só
        // essas produções (os empates resolvem-se aqui).
        var primeiros = _db.JobOns
            .Where(j => j.InicioEm != null && j.InicioEm > dia)
            .GroupBy(j => j.MachineId)
            .Select(g => new { MachineId = g.Key, InicioEm = g.Min(j => j.InicioEm) });
        var rows = await Project(_db.JobOns.AsNoTracking()
                .Where(j => primeiros.Any(p => p.MachineId == j.MachineId && p.InicioEm == j.InicioEm)))
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.Machine)
            .Select(g => g.MinBy(r => r.JobOnId)!)
            .ToList();
    }

    public async Task<PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(
        JobOnHistoryFilter filter,
        int? pageNumber,
        int pageSize = ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyHistoryFilter(_db.JobOns.AsNoTracking(), filter);
        var total = await filtered.CountAsync(cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        var jobs = filtered
            .OrderByDescending(j => j.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await Project(jobs).ToListAsync(cancellationToken);
        return new PagedList<ProductionContextReadModel>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(
        string reference,
        int? machineId = null,
        CancellationToken cancellationToken = default)
    {
        var term = reference.Trim().ToUpper();
        if (term.Length == 0)
        {
            return [];
        }
        var jobs = _db.JobOns.AsNoTracking().Where(j => j.Reference.ToUpper().Contains(term));
        if (machineId is int machine)
        {
            jobs = jobs.Where(j => j.MachineId == machine);
        }
        var rows = await jobs
            .OrderByDescending(j => j.Id)
            .Select(j => new { j.ProductionNumber, j.Reference })
            .Take(IProductionPickerQuery.MaxResults)
            .ToListAsync(cancellationToken);
        return rows
            .DistinctBy(r => r.ProductionNumber)
            .Select(r => new JobOnProductionOption(r.ProductionNumber, r.Reference))
            .ToList();
    }

    public async Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(
        string reference,
        int? pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var referencia = reference.Trim();
        var jobs = _db.JobOns.AsNoTracking().Where(j => j.Reference == referencia);
        var total = await jobs.CountAsync(cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        var items = await Project(jobs.OrderByDescending(j => j.Id).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new PagedList<ProductionContextReadModel>(items, total, page, pageSize);
    }

    // A referência contém o texto; a máquina e o nº de produção são iguais; o período apanha
    // as produções com dia cuja janela toca [desde, até] (sem fim = aberta).
    private static IQueryable<JobOn> ApplyHistoryFilter(IQueryable<JobOn> jobs, JobOnHistoryFilter filter)
    {
        if (filter.Reference?.Trim() is { Length: > 0 } reference)
        {
            var term = reference.ToUpper();
            jobs = jobs.Where(j => j.Reference.ToUpper().Contains(term));
        }
        if (filter.MachineId is int machineId)
        {
            jobs = jobs.Where(j => j.MachineId == machineId);
        }
        if (filter.ProductionNumber?.Trim() is { Length: > 0 } productionNumber)
        {
            jobs = jobs.Where(j => j.ProductionNumber == productionNumber);
        }
        if (filter.Desde is { } desde)
        {
            jobs = jobs.Where(j => j.InicioEm != null && (j.FimEm == null || j.FimEm >= desde));
        }
        if (filter.Ate is { } ate)
        {
            jobs = jobs.Where(j => j.InicioEm != null && j.InicioEm <= ate);
        }
        return jobs;
    }

    // A projeção comum: a máquina e os contextos aplicáveis (left joins),
    // a janela temporal; mais recente primeiro.
    private IQueryable<ProductionContextReadModel> Project(IQueryable<JobOn> jobs) =>
        from j in jobs
        join m in _db.Machines on j.MachineId equals m.Id
        join cm in _db.CmContexts on j.Id equals cm.JobOnId into cmGroup
        from cm in cmGroup.DefaultIfEmpty()
        join mf in _db.MfContexts on j.Id equals mf.JobOnId into mfGroup
        from mf in mfGroup.DefaultIfEmpty()
        join bq in _db.BqContexts on j.Id equals bq.JobOnId into bqGroup
        from bq in bqGroup.DefaultIfEmpty()
        orderby j.Id descending
        select new ProductionContextReadModel(
            j.Id,
            j.Reference,
            j.ProductionNumber,
            m.Code,
            cm != null ? cm.Id : (int?)null,
            cm != null ? cm.ToolId : null,
            mf != null ? mf.Id : (int?)null,
            mf != null ? mf.ToolId : null,
            bq != null ? bq.Id : (int?)null,
            bq != null ? bq.ToolId : null,
            j.InicioEm,
            j.FimEm);
}

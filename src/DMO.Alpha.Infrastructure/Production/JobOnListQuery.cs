using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
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

    public async Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(
        string reference,
        string? machineCode,
        CancellationToken cancellationToken = default)
    {
        // A referência inteira, igual à gravada: as referências gravam-se em
        // maiúsculas (CreateJobOnHandler), por isso o texto passa a maiúsculas
        // e a coluna fica como está (o índice da referência serve). «3540y079»
        // escrito à mão encontra a 3540Y079 (deep sweep 2, F-7).
        var referencia = reference.Trim().ToUpperInvariant();
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
        if (ReferenceMatch.Normalize(reference) is not { } term)
        {
            return [];
        }
        var jobs = _db.JobOns.AsNoTracking().Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, term));
        if (machineId is int machine)
        {
            jobs = jobs.Where(j => j.MachineId == machine);
        }
        // Cada linha é uma produção (o par referência + nº é único na base):
        // as últimas 5 são 5 opções, mesmo com o mesmo nº em duas referências
        // (deep sweep 2, F-17: os repetidos tirados depois do corte deixavam
        // menos de 5 e juntavam as duas referências numa opção).
        return await jobs
            .OrderByDescending(j => j.Id)
            .Select(j => new JobOnProductionOption(j.ProductionNumber, j.Reference))
            .Take(IProductionPickerQuery.MaxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(
        string reference,
        int? pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var referencia = reference.Trim().ToUpperInvariant();
        var jobs = _db.JobOns.AsNoTracking().Where(j => j.Reference == referencia);
        var total = await jobs.CountAsync(cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        var items = await Project(jobs.OrderByDescending(j => j.Id).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new PagedList<ProductionContextReadModel>(items, total, page, pageSize);
    }

    // A referência pelo início dela ou da parte BQ (ReferenceMatch); a máquina e o nº de
    // produção são iguais, e com a referência da produção escolhida só essa produção (o mesmo
    // nº pode existir noutra referência); o período apanha as produções com dia cuja janela
    // toca [desde, até] (sem fim = aberta).
    private static IQueryable<JobOn> ApplyHistoryFilter(IQueryable<JobOn> jobs, JobOnHistoryFilter filter)
    {
        if (ReferenceMatch.Normalize(filter.Reference) is { } reference)
        {
            jobs = jobs.Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, reference));
        }
        if (filter.MachineId is int machineId)
        {
            jobs = jobs.Where(j => j.MachineId == machineId);
        }
        if (filter.ProductionNumber?.Trim() is { Length: > 0 } productionNumber)
        {
            jobs = jobs.Where(j => j.ProductionNumber == productionNumber);
        }
        if (filter.ProductionReference?.Trim().ToUpperInvariant() is { Length: > 0 } productionReference)
        {
            jobs = jobs.Where(j => j.Reference == productionReference);
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
            JobOnId: j.Id,
            Reference: j.Reference,
            ProductionNumber: j.ProductionNumber,
            Machine: m.Code,
            CmId: cm != null ? cm.Id : (int?)null,
            CmToolId: cm != null ? cm.ToolId : null,
            MfId: mf != null ? mf.Id : (int?)null,
            MfToolId: mf != null ? mf.ToolId : null,
            BqId: bq != null ? bq.Id : (int?)null,
            BqToolId: bq != null ? bq.ToolId : null,
            InicioEm: j.InicioEm,
            FimEm: j.FimEm);
}

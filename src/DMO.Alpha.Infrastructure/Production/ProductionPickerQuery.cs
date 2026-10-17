using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação de <see cref="IProductionPickerQuery"/>: uma consulta curta
/// por referência (JobOns + Machines), filtrada e cortada na base.
/// </summary>
public sealed class ProductionPickerQuery : IProductionPickerQuery
{
    private readonly DmoDbContext _db;

    public ProductionPickerQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductionPickItem>> ListByReferenceAsync(
        string? reference,
        CancellationToken cancellationToken = default)
    {
        var term = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(term))
        {
            return Array.Empty<ProductionPickItem>();
        }

        // O filtro na entidade antes da projeção (o EF/Npgsql não traduz um
        // Where sobre o record projetado — o precedente do ResumoProductionQuery).
        return await (
                from j in _db.JobOns.AsNoTracking()
                where j.Reference.ToUpper().Contains(term)
                join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
                orderby j.Id descending
                select new ProductionPickItem(j.Id, j.ProductionNumber, j.Reference, m.Code, j.InicioEm))
            .Take(IProductionPickerQuery.MaxResults)
            .ToListAsync(cancellationToken);
    }

    public Task<ProductionPickItem?> FindAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        (from j in _db.JobOns.AsNoTracking()
         where j.Id == jobOnId
         join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
         select new ProductionPickItem(j.Id, j.ProductionNumber, j.Reference, m.Code, j.InicioEm))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReferenceSearchItem>> SearchReferencesAsync(
        string? term,
        CancellationToken cancellationToken = default)
    {
        var wanted = term?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(wanted))
        {
            return Array.Empty<ReferenceSearchItem>();
        }

        // As ferramentas cuja referência ou lote contêm o texto; as produções
        // que as usam entram na procura (subconsulta na base, nunca em memória).
        var tools = _db.Tools.AsNoTracking()
            .Where(t => t.Reference.ToUpper().Contains(wanted) || t.Lot.ToUpper().Contains(wanted))
            .Select(t => t.ToolId);
        var references = await _db.JobOns.AsNoTracking()
            .Where(j => j.Reference.ToUpper().Contains(wanted)
                || j.ProductionNumber.ToUpper().Contains(wanted)
                || _db.Machines.Any(m => m.Id == j.MachineId && m.Code.ToUpper().Contains(wanted))
                || _db.CmContexts.Any(c => c.JobOnId == j.Id && tools.Contains(c.ToolId))
                || _db.MfContexts.Any(c => c.JobOnId == j.Id && tools.Contains(c.ToolId))
                || _db.BqContexts.Any(c => c.JobOnId == j.Id && tools.Contains(c.ToolId)))
            .GroupBy(j => j.Reference)
            .Select(g => new { Reference = g.Key, Last = g.Max(j => j.Id) })
            .OrderByDescending(r => r.Last)
            .Take(IProductionPickerQuery.SearchLimit)
            .Select(r => r.Reference)
            .ToListAsync(cancellationToken);
        if (references.Count == 0)
        {
            return Array.Empty<ReferenceSearchItem>();
        }

        // Todas as produções das referências encontradas contam, não só as que
        // tinham o texto; a mais recente de cada uma numa consulta só.
        var totals = await _db.JobOns.AsNoTracking()
            .Where(j => references.Contains(j.Reference))
            .GroupBy(j => j.Reference)
            .Select(g => new { Reference = g.Key, Count = g.Count(), Last = g.Max(j => j.Id) })
            .ToListAsync(cancellationToken);
        var lastIds = totals.Select(t => t.Last).ToList();
        var lasts = await (
                from j in _db.JobOns.AsNoTracking()
                where lastIds.Contains(j.Id)
                join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
                select new { j.Id, j.ProductionNumber, Machine = m.Code, j.InicioEm })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        return totals
            .OrderByDescending(t => t.Last)
            .Select(t => new ReferenceSearchItem(
                t.Reference, t.Count, lasts[t.Last].ProductionNumber, lasts[t.Last].Machine, lasts[t.Last].InicioEm))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductionSearchItem>> ListOfReferenceAsync(
        string? reference,
        CancellationToken cancellationToken = default)
    {
        var wanted = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(wanted))
        {
            return Array.Empty<ProductionSearchItem>();
        }

        var found = await (
                from j in _db.JobOns.AsNoTracking()
                where j.Reference.ToUpper() == wanted
                join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
                orderby j.Id descending
                select new { j.Id, j.Reference, j.ProductionNumber, Machine = m.Code, j.InicioEm, j.FimEm })
            .Take(IProductionPickerQuery.SearchLimit)
            .ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return Array.Empty<ProductionSearchItem>();
        }

        // As ferramentas das produções encontradas: as ligações numa consulta
        // por tipo e os nomes numa só.
        var ids = found.Select(f => f.Id).ToList();
        var cmLinks = await _db.CmContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId))
            .Select(c => new ToolLink(c.JobOnId, c.ToolId)).ToListAsync(cancellationToken);
        var mfLinks = await _db.MfContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId))
            .Select(c => new ToolLink(c.JobOnId, c.ToolId)).ToListAsync(cancellationToken);
        var bqLinks = await _db.BqContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId))
            .Select(c => new ToolLink(c.JobOnId, c.ToolId)).ToListAsync(cancellationToken);
        var toolIds = cmLinks.Concat(mfLinks).Concat(bqLinks).Select(l => l.ToolId).Distinct().ToList();
        var labels = await _db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.Reference, t.Lot })
            .ToDictionaryAsync(
                t => t.ToolId,
                t => string.IsNullOrWhiteSpace(t.Lot) ? t.Reference : $"{t.Reference} · lote {t.Lot}",
                cancellationToken);

        string? LabelOf(List<ToolLink> links, int jobOnId) =>
            links.FirstOrDefault(l => l.JobOnId == jobOnId) is { } link
                ? labels.GetValueOrDefault(link.ToolId)
                : null;

        return found.Select(f => new ProductionSearchItem(
                f.Id, f.Reference, f.ProductionNumber, f.Machine, f.InicioEm, f.FimEm,
                LabelOf(cmLinks, f.Id), LabelOf(mfLinks, f.Id), LabelOf(bqLinks, f.Id)))
            .ToList();
    }

    private sealed record ToolLink(int JobOnId, string ToolId);
}

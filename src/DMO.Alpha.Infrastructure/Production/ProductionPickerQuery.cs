using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
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
        var term = ReferenceMatch.Normalize(reference);
        if (term is null)
        {
            return Array.Empty<ProductionPickItem>();
        }

        // O filtro na entidade antes da projeção (o EF/Npgsql não traduz um
        // Where sobre o record projetado — o precedente do ResumoProductionQuery).
        // O INÍCIO DA REFERÊNCIA OU DA PARTE BQ (Diogo, 18/10/2026, a regra de
        // ReferenceMatch), nunca um pedaço do meio: «35» trazia a 4996T350 e
        // «2847» outras referências com 2847 no meio (deep sweep 2, F-1/F-3).
        // Não só a referência inteira: o início («6018») já enche o dropdown
        // das produções, como na procura do Job On (Diogo, beta.208; beta.233).
        // Cada item leva o seu jobon_id: o nº só é único junto com a referência.
        return await (
                from j in _db.JobOns.AsNoTracking().Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, term))
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

    public async Task<IReadOnlyList<ProductionSearchItem>> ListOfReferenceAsync(
        string? reference,
        CancellationToken cancellationToken = default)
    {
        var wanted = ReferenceMatch.Normalize(reference);
        if (wanted is null)
        {
            return Array.Empty<ProductionSearchItem>();
        }

        // SÓ A REFERÊNCIA COMPLETA (Diogo, 20/10/2026: «primeiro campo filtra
        // apenas referências completas, segundo aparece dropdown das produções
        // 202601 202602»): o início da referência é para as sugestões por
        // baixo do campo; o dropdown só se enche com uma referência inteira.
        var found = await (
                from j in _db.JobOns.AsNoTracking().Where(j => j.Reference != null && j.Reference.ToUpper() == wanted)
                join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
                // A mais recente primeiro (Diogo, 26/10/2026): pelo início, depois pelo
                // nº da produção; sem início vão para o fim.
                orderby j.InicioEm == null, j.InicioEm descending, j.ProductionNumber descending, j.Id descending
                select new { j.Id, j.Reference, j.ProductionNumber, Machine = m.Code, j.InicioEm, j.FimEm })
            .Take(IProductionPickerQuery.ProductionsOfReferenceLimit)
            .ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return Array.Empty<ProductionSearchItem>();
        }

        // As ferramentas das produções encontradas: as ligações numa consulta
        // por tipo e os nomes numa só.
        var ids = found.Select(f => f.Id).ToList();
        var cmLinks = await _db.CmContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
            .Select(c => new ToolLink(c.JobOnId!.Value, c.ToolId)).ToListAsync(cancellationToken);
        var mfLinks = await _db.MfContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
            .Select(c => new ToolLink(c.JobOnId!.Value, c.ToolId)).ToListAsync(cancellationToken);
        var bqLinks = await _db.BqContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
            .Select(c => new ToolLink(c.JobOnId!.Value, c.ToolId)).ToListAsync(cancellationToken);
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

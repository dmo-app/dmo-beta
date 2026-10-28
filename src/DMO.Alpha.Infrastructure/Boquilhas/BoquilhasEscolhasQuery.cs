using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// As leituras de escolha e de filtro que a página Boquilhas fazia ela própria
/// contra o contexto: as mesmas consultas, só leitura (AsNoTracking).
/// </summary>
public sealed class BoquilhasEscolhasQuery : IBoquilhasEscolhasQuery
{
    private readonly DmoDbContext _db;

    public BoquilhasEscolhasQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BqJobOnAnterior>> BqJobOnsAnterioresAsync(CancellationToken cancellationToken = default) =>
        await (
                from c in _db.BqContexts.AsNoTracking()
                join j in _db.JobOns.AsNoTracking() on c.JobOnId ?? c.ReplacedJobOnId equals (int?)j.Id
                where c.ToolId != ToolRegistryConstants.RemovedToolId
                orderby j.InicioEm descending, j.Id descending
                select new BqJobOnAnterior(c.Id, c.ToolId, j.Id, j.Reference, j.ProductionNumber, j.InicioEm, j.MachineId, j.Machine.Code))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BqSemJobOnLida>> BqSemJobOnAsync(CancellationToken cancellationToken = default)
    {
        var lidas = await _db.Tools.AsNoTracking()
            .Where(t => t.Type == ToolType.Bq && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .OrderBy(t => t.Reference).ThenBy(t => t.Lot)
            .Select(t => new
            {
                t.ToolId, t.Reference, t.Lot, t.BqAccountedQuantity, t.AssociatedMfReference,
                Machines = _db.ToolMachineLinks.Where(l => l.ToolId == t.ToolId)
                    .Select(l => l.Machine.Code).OrderBy(c => c).ToList(),
                LastLine = _db.BqMovements
                    .Where(m => m.Trace.ToolId == t.ToolId && m.MachineCodeFrozen != null)
                    .OrderByDescending(m => m.OccurredAtUtc)
                    .Select(m => m.MachineCodeFrozen)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return lidas
            .Select(t => new BqSemJobOnLida(t.ToolId, t.Reference, t.Lot, t.BqAccountedQuantity,
                t.Machines, t.AssociatedMfReference, t.LastLine))
            .ToList();
    }

    public Task<BqIdentificada?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default) =>
        _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new BqIdentificada(t.ToolId, t.Reference, t.Lot))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BqIdentificada>> BqsDaReferenciaAsync(
        string referenciaEmMaiusculas, CancellationToken cancellationToken = default) =>
        await _db.Tools
            .AsNoTracking()
            .Where(t => t.Type == ToolType.Bq && t.Reference.ToUpper() == referenciaEmMaiusculas && t.Lot != null)
            .Select(t => new BqIdentificada(t.ToolId, t.Reference, t.Lot))
            .ToListAsync(cancellationToken);

    public Task<ProducaoDoFiltro?> ProducaoDoFiltroAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        _db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new ProducaoDoFiltro(j.Id, j.ProductionNumber, j.Reference, j.Machine.Code))
            .FirstOrDefaultAsync(cancellationToken);
}

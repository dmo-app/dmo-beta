using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação purpose-specific do AVISO de eliminação de uma Tool: as
/// produções cujos contextos (Cm/Mf/BqContext — strings penduradas sem FK
/// para Tools, verdade histórica) referem o tool_id, e as contagens dos
/// registos de consumo que a transação de eliminação reatribui à DUMMY
/// (Pesos, Pegamentos) ou cuja âncora direta limpa (traces não
/// resolvidos). A existência de histórico é motivo de AVISO e
/// confirmação explícita — nunca recusa automática (blueprint L145/L168);
/// esta lista cobre a decisão informada do Owner. Só de leitura,
/// AsNoTracking; a DUMMY nunca resolve (não é eliminável).
/// </summary>
public sealed class ToolDeletionImpactQuery : IToolDeletionImpactQuery
{
    private readonly DmoDbContext _db;

    public ToolDeletionImpactQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<ToolDeletionImpactReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId)
            || toolId == ToolRegistryConstants.RemovedToolId)
        {
            return null;
        }

        // Uma só consulta: as produções cujo contexto de qualquer tipo
        // referencia o tool_id (mais recentes primeiro; o delete não toca nos
        // contextos), os Pesos e Pegamentos reatribuídos à DUMMY e os traces
        // não resolvidos cuja âncora direta será limpa.
        var impact = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new
            {
                Productions = (
                    from jobOn in _db.JobOns
                    join machine in _db.Machines on jobOn.MachineId equals machine.Id
                    // Também as produções em que a CM, o MF ou a BQ foi trocado por outro.
                    where _db.CmContexts.Any(c => (c.JobOnId == jobOn.Id || c.ReplacedJobOnId == jobOn.Id) && c.ToolId == t.ToolId)
                        || _db.MfContexts.Any(c => (c.JobOnId == jobOn.Id || c.ReplacedJobOnId == jobOn.Id) && c.ToolId == t.ToolId)
                        || _db.BqContexts.Any(c => (c.JobOnId == jobOn.Id || c.ReplacedJobOnId == jobOn.Id) && c.ToolId == t.ToolId)
                    orderby jobOn.Id descending
                    select new ToolDeletionImpactProduction(
                        jobOn.Id,
                        jobOn.ProductionNumber,
                        jobOn.Reference,
                        machine.Code))
                    .ToList(),
                PesoCount = _db.Pesos.Count(p => p.CmToolId == t.ToolId || p.BqToolId == t.ToolId),
                PegamentosCount = _db.Pegamentos.Count(p => p.CmToolId == t.ToolId
                    || p.BqToolId == t.ToolId
                    || p.MfToolId == t.ToolId),
                UnresolvedTraceCount = _db.BqRepairTraces.Count(r => r.ToolId == t.ToolId && r.BqContextId == null)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (impact is null)
        {
            return null;
        }

        return new ToolDeletionImpactReadModel(
            toolId,
            impact.Productions,
            impact.PesoCount,
            impact.PegamentosCount,
            impact.UnresolvedTraceCount);
    }
}

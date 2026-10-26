using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// As leituras dos registos de uma BQ e da produção a associar que a página
/// Boquilhas fazia ela própria contra o contexto: as mesmas consultas, só
/// leitura (AsNoTracking).
/// </summary>
public sealed class BoquilhasAssociacaoQuery : IBoquilhasAssociacaoQuery
{
    private readonly DmoDbContext _db;

    public BoquilhasAssociacaoQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<PagedList<BqRegistoDaBq>> RegistosDaBqAsync(
        string toolId, int? pageNumber, CancellationToken cancellationToken = default)
    {
        var traces = await _db.BqRepairTraces
            .AsNoTracking()
            .Where(t => t.ToolId == toolId
                || (t.BqContext != null && t.BqContext.ToolId == toolId))
            .OrderByDescending(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.BqContextId,
                // A BQ deste registo foi trocada no Job On (18/10/2026): a
                // produção pode estar em máquina, mas já com outra BQ.
                Trocada = t.BqContext != null && t.BqContext.JobOnId == null,
                // Um bq_id de uma BQ trocada continua a mostrar a produção.
                ProductionNumber = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.ProductionNumber : t.BqContext.ReplacedJobOn!.ProductionNumber,
                ProductionReference = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.Reference : t.BqContext.ReplacedJobOn!.Reference,
                MachineCode = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.Machine.Code : t.BqContext.ReplacedJobOn!.Machine.Code,
                InicioEm = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.InicioEm : t.BqContext.ReplacedJobOn!.InicioEm,
                FimEm = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.FimEm : t.BqContext.ReplacedJobOn!.FimEm
            })
            .ToPagedListAsync(pageNumber, cancellationToken: cancellationToken);

        // As contagens só dos registos da página.
        var traceIds = traces.Items.Select(t => t.Id).ToList();
        var movementCounts = await _db.BqMovements
            .AsNoTracking()
            .Where(m => traceIds.Contains(m.BqRepairTraceId))
            .GroupBy(m => m.BqRepairTraceId)
            .Select(g => new { TraceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TraceId, g => g.Count, cancellationToken);

        return new PagedList<BqRegistoDaBq>(traces.Items.Select(t => new BqRegistoDaBq(
            t.Id,
            t.BqContextId,
            t.Trocada,
            t.ProductionNumber,
            t.ProductionReference,
            t.MachineCode,
            t.InicioEm,
            t.FimEm,
            movementCounts.TryGetValue(t.Id, out var count) ? count : 0)).ToList(),
            traces.TotalCount, traces.PageNumber, traces.PageSize);
    }

    public Task<BqRegistoPendente?> RegistoSemJobOnAsync(string toolId, CancellationToken cancellationToken = default) =>
        _db.BqRepairTraces
            .AsNoTracking()
            .Where(t => t.ToolId == toolId && t.BqContextId == null)
            .Select(t => new BqRegistoPendente(t.Id, _db.BqMovements.Count(m => m.BqRepairTraceId == t.Id)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BqProducaoParaAssociar?> ProducaoAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        _db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new BqProducaoParaAssociar(j.Id, j.Reference))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BqProducaoComBq>> ProducoesDoNumeroAsync(
        string productionNumber, string? toolId, CancellationToken cancellationToken = default) =>
        await _db.JobOns
            .AsNoTracking()
            .Where(j => j.ProductionNumber == productionNumber)
            .OrderByDescending(j => j.Id)
            .Select(j => new BqProducaoComBq(
                j.Id,
                j.Reference,
                _db.BqContexts.Any(c => c.JobOnId == j.Id && c.ToolId == toolId)))
            .ToListAsync(cancellationToken);

    public async Task<int?> ContextoDaBqNaProducaoAsync(
        int jobOnId, string? toolId, CancellationToken cancellationToken = default) =>
        await _db.BqContexts
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId && c.ToolId == toolId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

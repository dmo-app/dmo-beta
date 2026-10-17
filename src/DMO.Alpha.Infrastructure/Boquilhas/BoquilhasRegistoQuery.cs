using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Serviço de leitura do Registo de Boquilhas: resolução cirúrgica por
/// bq_repair_trace_id (sem scans globais, projeções mínimas). Resolve os
/// factos referência/lote da Tool canónica quando existem no registo, o
/// contexto de produção Job On alcançável através do bq_id opcional, os
/// movimentos persistidos do trace e apenas as quantidades físicas
/// derivadas cujas regras de movimento já estão fechadas: a quantidade
/// legitimamente fora (cada saida acrescenta; cada retorno — entrada ou
/// entrada_sem_reparacao — devolve apenas a porção explicável) e a
/// discrepância acumulada (soma das discrepâncias persistidas, null
/// quando nenhuma). O Tool.Type nunca é um gate: o trace já transporta o
/// tool_id relevante.
/// </summary>
public sealed class BoquilhasRegistoQuery : IBoquilhasRegistoQuery
{
    private readonly DmoDbContext _db;

    public BoquilhasRegistoQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<BoquilhasRegistoTrace?> FindTraceAsync(
        int bqRepairTraceId,
        CancellationToken cancellationToken = default)
    {
        // O trace, a produção do bq_id e a Tool efetiva numa consulta.
        var header = await (
            from trace in _db.BqRepairTraces.AsNoTracking()
            where trace.Id == bqRepairTraceId
            join context in _db.BqContexts.AsNoTracking() on trace.BqContextId equals context.Id into contexts
            from context in contexts.DefaultIfEmpty()
            join jobOn in _db.JobOns.AsNoTracking() on (context != null ? context.JobOnId : (int?)null) equals jobOn.Id into jobOns
            from jobOn in jobOns.DefaultIfEmpty()
            join machine in _db.Machines.AsNoTracking() on (jobOn != null ? jobOn.MachineId : (int?)null) equals machine.Id into machines
            from machine in machines.DefaultIfEmpty()
            // tool_id efetivo: a âncora direta enquanto o trace está
            // pré-produção; depois da associação, bq_id → BqContext.ToolId.
            join tool in _db.Tools.AsNoTracking().Where(x => x.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId)
                on (trace.ToolId != null ? trace.ToolId : context != null ? context.ToolId : null) equals tool.ToolId into tools
            from tool in tools.DefaultIfEmpty()
            select new TraceHeader(
                trace.Id,
                trace.ToolId != null ? trace.ToolId : context != null ? context.ToolId : null,
                trace.BqContextId,
                tool != null ? tool.Reference : null,
                tool != null ? tool.Lot : null,
                tool != null ? tool.BqAccountedQuantity : null,
                jobOn != null ? jobOn.Reference : null,
                jobOn != null ? jobOn.ProductionNumber : null,
                machine != null ? machine.Code : null,
                jobOn != null ? jobOn.InicioEm : null,
                jobOn != null ? jobOn.FimEm : null))
            .FirstOrDefaultAsync(cancellationToken);

        return header is null ? null : await ComposeTraceAsync(header, cancellationToken);
    }

    public async Task<BoquilhasRegistoContext?> FindByContextAsync(
        int bqContextId,
        CancellationToken cancellationToken = default)
    {
        // O bq_id, a produção, o trace associado (um bq_id tem um único
        // trace) e a ferramenta numa consulta.
        var row = await (
            from context in _db.BqContexts.AsNoTracking()
            where context.Id == bqContextId
            join jobOn in _db.JobOns.AsNoTracking() on context.JobOnId equals jobOn.Id
            join machine in _db.Machines.AsNoTracking() on jobOn.MachineId equals machine.Id
            join trace in _db.BqRepairTraces.AsNoTracking() on (int?)context.Id equals trace.BqContextId into traces
            from trace in traces.DefaultIfEmpty()
            join tool in _db.Tools.AsNoTracking().Where(x => x.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId)
                on (trace != null && trace.ToolId != null ? trace.ToolId : context.ToolId) equals tool.ToolId into tools
            from tool in tools.DefaultIfEmpty()
            select new
            {
                ContextToolId = context.ToolId,
                TraceId = trace != null ? trace.Id : (int?)null,
                TraceToolId = trace != null && trace.ToolId != null ? trace.ToolId : context.ToolId,
                ToolType = tool != null ? (DMO.Alpha.Core.Tools.ToolType?)tool.Type : null,
                ToolReference = tool != null ? tool.Reference : null,
                ToolLot = tool != null ? tool.Lot : null,
                ToolAccountedQuantity = tool != null ? tool.BqAccountedQuantity : null,
                jobOn.Reference,
                jobOn.ProductionNumber,
                MachineCode = machine.Code,
                jobOn.InicioEm,
                jobOn.FimEm
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        BoquilhasRegistoTrace? associated = null;
        if (row.TraceId is int traceId)
        {
            associated = await ComposeTraceAsync(
                new TraceHeader(
                    traceId,
                    row.TraceToolId,
                    bqContextId,
                    row.ToolReference,
                    row.ToolLot,
                    row.ToolAccountedQuantity,
                    row.Reference,
                    row.ProductionNumber,
                    row.MachineCode,
                    row.InicioEm,
                    row.FimEm),
                cancellationToken);
        }

        // A ferramenta do contexto como no Ferramentas: a linha REMOVED não
        // conta como registada.
        var contextTool = row.ToolType is { } toolType
            && row.TraceToolId == row.ContextToolId
            && row.ContextToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId
            ? new DMO.Alpha.Core.Tools.ToolLookupResult(row.ContextToolId, toolType, row.ToolReference!, row.ToolLot!)
            : null;

        return new BoquilhasRegistoContext(
            bqContextId,
            row.ContextToolId,
            new BoquilhasRegistoProductionContext(row.Reference, row.ProductionNumber, row.MachineCode, row.InicioEm, row.FimEm),
            contextTool,
            contextTool is null ? null : row.ToolAccountedQuantity,
            associated);
    }

    /// <summary>A linha do trace com a Tool efetiva e a produção do bq_id.</summary>
    private sealed record TraceHeader(
        int TraceId,
        string? ToolId,
        int? BqContextId,
        string? ToolReference,
        string? ToolLot,
        int? ToolAccountedQuantity,
        string? ProductionReference,
        string? ProductionNumber,
        string? MachineCode,
        DateOnly? InicioEm,
        DateOnly? FimEm);

    /// <summary>
    /// O read model a partir da linha do trace: os movimentos (uma consulta)
    /// com as quantidades físicas derivadas.
    /// </summary>
    private async Task<BoquilhasRegistoTrace> ComposeTraceAsync(
        TraceHeader header,
        CancellationToken cancellationToken)
    {
        var traceId = header.TraceId;
        var production = header.ProductionReference is not null
            && header.ProductionNumber is not null
            && header.MachineCode is not null
            ? new BoquilhasRegistoProductionContext(header.ProductionReference, header.ProductionNumber, header.MachineCode, header.InicioEm, header.FimEm)
            : null;

        var movements = await _db.BqMovements
            .AsNoTracking()
            .Where(movement => movement.BqRepairTraceId == traceId)
            // OS ÚLTIMOS REGISTOS primeiro (o Owner 2026-10-13: «por baixo
            // mostrar os ultimos registos» — a atividade mais recente no topo),
            // pela data real e depois pelo Id: a mesma ordem da regra «apagar
            // os 5 mais recentes».
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Select(movement => new
            {
                movement.Id,
                movement.Type,
                movement.Quantity,
                movement.Discrepancy,
                movement.OccurredAtUtc,
                movement.BqRepairerNameFrozen,
                movement.MachineCodeFrozen,
                movement.CorrectedByNameFrozen,
                movement.CorrectedAtUtc
            })
            .ToListAsync(cancellationToken);

        // Quantidades físicas derivadas, apenas onde as regras já fecham o
        // cálculo: quantidade legitimamente fora (matched/unmatched) e
        // discrepância acumulada do trace. A FOLD É CRONOLÓGICA (pela data
        // real, depois pelo Id — a mesma ordem do BqDiscrepancyFold); o
        // DISPLAY da lista é DESC (o Owner: «por baixo mostrar os ultimos
        // registos») — as duas ordens são separadas.
        var quantityLegitimatelyOut = 0;
        var accumulatedDiscrepancy = 0;
        var hasDiscrepancy = false;
        foreach (var movement in movements.OrderBy(movement => movement.OccurredAtUtc).ThenBy(movement => movement.Id))
        {
            if (movement.Type == BqMovementType.Saida)
            {
                quantityLegitimatelyOut += movement.Quantity;
            }
            else
            {
                var matched = Math.Min(movement.Quantity, quantityLegitimatelyOut);
                quantityLegitimatelyOut -= matched;
            }

            if (movement.Discrepancy is int discrepancy)
            {
                accumulatedDiscrepancy += discrepancy;
                hasDiscrepancy = true;
            }
        }

        return new BoquilhasRegistoTrace(
            traceId,
            header.ToolId,
            header.ToolReference,
            header.ToolLot,
            header.BqContextId,
            production,
            movements.Select(movement => new BoquilhasRegistoMovement(
                movement.Id,
                BqMovementTypeTokens.ToStorage(movement.Type),
                movement.Quantity,
                movement.Discrepancy,
                movement.OccurredAtUtc,
                movement.BqRepairerNameFrozen,
                movement.MachineCodeFrozen,
                movement.CorrectedByNameFrozen,
                movement.CorrectedAtUtc)).ToList(),
            quantityLegitimatelyOut,
            hasDiscrepancy ? accumulatedDiscrepancy : null,
            header.ToolAccountedQuantity);
    }
}

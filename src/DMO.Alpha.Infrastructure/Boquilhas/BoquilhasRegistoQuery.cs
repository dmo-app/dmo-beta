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
        int? movementPage = null,
        CancellationToken cancellationToken = default)
    {
        // O trace, a produção do bq_id e a Tool efetiva numa consulta.
        var header = await (
            from trace in _db.BqRepairTraces.AsNoTracking()
            where trace.Id == bqRepairTraceId
            join context in _db.BqContexts.AsNoTracking() on trace.BqContextId equals context.Id into contexts
            from context in contexts.DefaultIfEmpty()
            join jobOn in _db.JobOns.AsNoTracking() on (context != null ? context.JobOnId ?? context.ReplacedJobOnId : null) equals jobOn.Id into jobOns
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
                jobOn != null ? jobOn.FimEm : null,
                trace.DiscrepancyResetAtUtc,
                trace.DiscrepancyResetByNameFrozen,
                jobOn != null ? jobOn.Id : (int?)null))
            .FirstOrDefaultAsync(cancellationToken);

        return header is null ? null : await ComposeTraceAsync(header, movementPage, cancellationToken);
    }

    public async Task<BoquilhasRegistoMovement?> FindMovementAsync(
        int bqRepairTraceId,
        int movementId,
        CancellationToken cancellationToken = default)
    {
        var movement = await _db.BqMovements
            .AsNoTracking()
            .Where(m => m.Id == movementId && m.BqRepairTraceId == bqRepairTraceId)
            .FirstOrDefaultAsync(cancellationToken);
        return movement is null ? null : new BoquilhasRegistoMovement(
            movement.Id,
            BqMovementTypeTokens.ToStorage(movement.Type),
            movement.Quantity,
            movement.Discrepancy,
            movement.OccurredAtUtc,
            movement.BqRepairerNameFrozen,
            movement.MachineCodeFrozen,
            movement.CorrectedByNameFrozen,
            movement.CorrectedAtUtc);
    }

    public async Task<BoquilhasRegistoContext?> FindByContextAsync(
        int bqContextId,
        int? movementPage = null,
        CancellationToken cancellationToken = default)
    {
        // O bq_id, a produção, o trace associado (um bq_id tem um único
        // trace) e a ferramenta numa consulta.
        var row = await (
            from context in _db.BqContexts.AsNoTracking()
            where context.Id == bqContextId
            join jobOn in _db.JobOns.AsNoTracking() on context.JobOnId ?? context.ReplacedJobOnId equals jobOn.Id
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
                TraceResetAtUtc = trace != null ? trace.DiscrepancyResetAtUtc : null,
                TraceResetBy = trace != null ? trace.DiscrepancyResetByNameFrozen : null,
                TraceToolId = trace != null && trace.ToolId != null ? trace.ToolId : context.ToolId,
                ToolType = tool != null ? (DMO.Alpha.Core.Tools.ToolType?)tool.Type : null,
                ToolReference = tool != null ? tool.Reference : null,
                ToolLot = tool != null ? tool.Lot : null,
                ToolAccountedQuantity = tool != null ? tool.BqAccountedQuantity : null,
                jobOn.Reference,
                jobOn.ProductionNumber,
                MachineCode = machine.Code,
                jobOn.InicioEm,
                jobOn.FimEm,
                JobOnId = jobOn.Id
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
                    row.FimEm,
                    row.TraceResetAtUtc,
                    row.TraceResetBy,
                    row.JobOnId),
                movementPage,
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
            new BoquilhasRegistoProductionContext(row.Reference, row.ProductionNumber, row.MachineCode, row.InicioEm, row.FimEm, row.JobOnId),
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
        DateOnly? FimEm,
        DateTime? DiscrepancyResetAtUtc = null,
        string? DiscrepancyResetBy = null,
        int? JobOnId = null);

    /// <summary>
    /// O read model a partir da linha do trace: as contas leem só os números
    /// de todos os movimentos (uma consulta leve); a página pedida lê as
    /// linhas completas de 25 movimentos (outra consulta).
    /// </summary>
    private async Task<BoquilhasRegistoTrace> ComposeTraceAsync(
        TraceHeader header,
        int? movementPage,
        CancellationToken cancellationToken)
    {
        var traceId = header.TraceId;
        var production = header.ProductionReference is not null
            && header.ProductionNumber is not null
            && header.MachineCode is not null
            ? new BoquilhasRegistoProductionContext(header.ProductionReference, header.ProductionNumber, header.MachineCode, header.InicioEm, header.FimEm, header.JobOnId)
            : null;

        var movements = await _db.BqMovements
            .AsNoTracking()
            .Where(movement => movement.BqRepairTraceId == traceId)
            .Select(movement => new
            {
                movement.Id,
                movement.Type,
                movement.Quantity,
                movement.Discrepancy,
                movement.OccurredAtUtc,
                movement.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        // Quantidades físicas derivadas, apenas onde as regras já fecham o
        // cálculo: quantidade legitimamente fora (matched/unmatched) e
        // discrepância acumulada do trace. A FOLD É CRONOLÓGICA (a mesma
        // ordem do BqDiscrepancyFold: data real, a saída antes da entrada à
        // mesma hora, depois o Id); a LISTA mostra a ordem inversa, os
        // últimos registos no topo (o Owner 2026-10-13: «por baixo mostrar
        // os ultimos registos»).
        var chronological = movements
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => BqDiscrepancyFold.SaidaFirst(movement.Type))
            .ThenBy(movement => movement.Id)
            .ToList();
        var quantityLegitimatelyOut = 0;
        var accumulatedDiscrepancy = 0;
        var hasDiscrepancy = false;
        foreach (var movement in chronological)
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

            // Depois de posta a zero, a discrepância acumulada só conta os
            // movimentos com data real posterior (Diogo, 17/10/2026).
            if (movement.Discrepancy is int discrepancy
                && (header.DiscrepancyResetAtUtc is not DateTime resetAt
                    || BqDiscrepancyFold.CountsAfterReset(movement.OccurredAtUtc, movement.CreatedAtUtc, resetAt)))
            {
                accumulatedDiscrepancy += discrepancy;
                hasDiscrepancy = true;
            }
        }

        chronological.Reverse();

        // A página da tabela: os ids saem da mesma ordem e só esses 25
        // movimentos são lidos por inteiro.
        var pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize;
        var page = DMO.Alpha.Core.Paging.ListPaging.ClampPage(movementPage, chronological.Count, pageSize);
        var pageIds = chronological.Skip((page - 1) * pageSize).Take(pageSize).Select(movement => movement.Id).ToList();
        var pageRows = pageIds.Count == 0
            ? new Dictionary<int, BqMovement>()
            : await _db.BqMovements
                .AsNoTracking()
                .Where(movement => pageIds.Contains(movement.Id))
                .ToDictionaryAsync(movement => movement.Id, cancellationToken);

        return new BoquilhasRegistoTrace(
            traceId,
            header.ToolId,
            header.ToolReference,
            header.ToolLot,
            header.BqContextId,
            production,
            pageIds.Select(id => pageRows[id]).Select(movement => new BoquilhasRegistoMovement(
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
            hasDiscrepancy || header.DiscrepancyResetAtUtc is not null ? accumulatedDiscrepancy : null,
            header.ToolAccountedQuantity,
            header.DiscrepancyResetAtUtc,
            header.DiscrepancyResetBy,
            chronological.Count,
            page);
    }
}

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
                tool != null ? tool.AnQuantity : null,
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
            movement.CorrectedAtUtc,
            movement.AnQuantity);
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
                ToolAnQuantity = tool != null ? tool.AnQuantity : null,
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
                    row.ToolAnQuantity,
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
            associated,
            contextTool is null ? null : row.ToolAnQuantity);
    }

    /// <summary>A linha do trace com a Tool efetiva e a produção do bq_id.</summary>
    private sealed record TraceHeader(
        int TraceId,
        string? ToolId,
        int? BqContextId,
        string? ToolReference,
        string? ToolLot,
        int? ToolAccountedQuantity,
        int? ToolAnQuantity,
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
                movement.AnQuantity,
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
        // A conta das BQ e a dos AN (só com os movimentos que levam AN) é da
        // BQ em todas as produções (Diogo, 28/10/2026: «só deve criar saldo
        // quando entra mais do que saiu»): uma BQ que saiu num Job On e volta
        // registada noutro não fica a mais. Faz-se aqui, e não com o valor
        // gravado em cada movimento, que pode ser de antes da regra.
        var outros = header.ToolId is not { } bqToolId
            ? []
            : await _db.BqMovements.AsNoTracking()
                .Where(m => m.BqRepairTraceId != traceId && BqDiscrepancyFold.RegistosDaBq(_db, bqToolId).Contains(m.BqRepairTraceId))
                .Select(m => new { m.Id, m.Type, m.Quantity, m.AnQuantity, m.OccurredAtUtc })
                .ToListAsync(cancellationToken);
        var daBq = chronological.Select(m => (m.Id, m.Type, m.Quantity, m.AnQuantity, m.OccurredAtUtc))
            .Concat(outros.Select(m => (m.Id, m.Type, m.Quantity, m.AnQuantity, m.OccurredAtUtc)))
            .OrderBy(m => m.OccurredAtUtc)
            .ThenBy(m => BqDiscrepancyFold.SaidaFirst(m.Type))
            .ThenBy(m => m.Id)
            .ToList();
        var (bqFold, quantityLegitimatelyOut) = BqDiscrepancyFold.Fold(
            daBq.Select(m => (m.Type == BqMovementType.Saida, m.Quantity)).ToList());
        var discrepancies = new Dictionary<int, int>();
        for (var i = 0; i < daBq.Count; i++)
        {
            if (bqFold[i] is int d)
            {
                discrepancies[daBq[i].Id] = d;
            }
        }

        var comAn = daBq.Where(m => m.AnQuantity is not null).ToList();
        var hasAn = comAn.Count > 0;
        var (anFold, anOut) = BqDiscrepancyFold.Fold(
            comAn.Select(m => (m.Type == BqMovementType.Saida, m.AnQuantity!.Value)).ToList());
        var anDiscrepancies = new Dictionary<int, int>();
        for (var i = 0; i < comAn.Count; i++)
        {
            if (anFold[i] is int d)
            {
                anDiscrepancies[comAn[i].Id] = d;
            }
        }

        // Depois de posta a zero, a discrepância acumulada só conta os
        // movimentos com data real posterior (Diogo, 17/10/2026).
        bool ContaDepoisDoZero(DateTime occurredAtUtc, DateTime createdAtUtc) =>
            header.DiscrepancyResetAtUtc is not DateTime resetAt
            || BqDiscrepancyFold.CountsAfterReset(occurredAtUtc, createdAtUtc, resetAt);
        var contam = chronological.Where(m => ContaDepoisDoZero(m.OccurredAtUtc, m.CreatedAtUtc)).Select(m => m.Id).ToHashSet();
        var hasDiscrepancy = discrepancies.Keys.Any(contam.Contains);
        var accumulatedDiscrepancy = discrepancies.Where(d => contam.Contains(d.Key)).Sum(d => d.Value);
        var hasAnDiscrepancy = anDiscrepancies.Keys.Any(contam.Contains);
        var anAccumulated = anDiscrepancies.Where(d => contam.Contains(d.Key)).Sum(d => d.Value);

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
        var userIds = pageRows.Values.Select(m => m.RegisteredByUserId).OfType<Guid>().Distinct().ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        string? QuemRegistou(BqMovement movement) =>
            movement.RegisteredByUserId is Guid userId && userNames.TryGetValue(userId, out var nome) && !string.IsNullOrWhiteSpace(nome)
                ? nome
                : movement.RegisteredByOperatorId;

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
                // Meter a zero limpa o Saldo das linhas anteriores ao zero
                // (Diogo, 26/10/2026); os movimentos e o valor gravado ficam.
                header.DiscrepancyResetAtUtc is DateTime shownResetAt
                    && !BqDiscrepancyFold.CountsAfterReset(movement.OccurredAtUtc, movement.CreatedAtUtc, shownResetAt)
                    ? null
                    : discrepancies.TryGetValue(movement.Id, out var bqDisc) ? bqDisc : null,
                movement.OccurredAtUtc,
                movement.BqRepairerNameFrozen,
                movement.MachineCodeFrozen,
                movement.CorrectedByNameFrozen,
                movement.CorrectedAtUtc,
                movement.AnQuantity,
                header.DiscrepancyResetAtUtc is DateTime anShownResetAt
                    && !BqDiscrepancyFold.CountsAfterReset(movement.OccurredAtUtc, movement.CreatedAtUtc, anShownResetAt)
                    ? null
                    : anDiscrepancies.TryGetValue(movement.Id, out var anDisc) ? anDisc : null,
                QuemRegistou(movement))).ToList(),
            quantityLegitimatelyOut,
            hasDiscrepancy || header.DiscrepancyResetAtUtc is not null ? accumulatedDiscrepancy : null,
            header.ToolAccountedQuantity,
            header.DiscrepancyResetAtUtc,
            header.DiscrepancyResetBy,
            chronological.Count,
            page,
            header.ToolAnQuantity,
            hasAn ? anOut : null,
            hasAnDiscrepancy || (hasAn && header.DiscrepancyResetAtUtc is not null) ? anAccumulated : null);
    }
}

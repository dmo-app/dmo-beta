using DMO.Alpha.Core.Boquilhas;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// A máquina aberta sem registo (decisão do proprietário de 16/10/2026):
/// abrir a máquina no rail já não cria o registo de reparação. A folha mostra
/// o mesmo estado (a produção, o histórico vazio, o form do movimento) sem
/// escrever nada; o form envia o bq_id e o primeiro movimento cria o registo.
/// </summary>
public sealed partial class IndexModel
{
    /// <summary>O bq_id da produção aberta que ainda não tem registo; null nos outros casos.</summary>
    public int? PendingRegisterBqContextId { get; private set; }

    /// <summary>
    /// O registo pré-produção (sem Job On) da mesma boquilha, quando existe:
    /// o primeiro movimento desta produção associa-o, como antes fazia a
    /// abertura da máquina. A folha avisa; nada bloqueia.
    /// </summary>
    public int? PendingUnresolvedTraceId { get; private set; }

    public int PendingUnresolvedMovementCount { get; private set; }

    /// <summary>
    /// Compõe a folha da produção sem registo: um registo por gravar (id 0,
    /// sem movimentos) com a referência e o lote da boquilha. Sem a
    /// ferramenta no Ferramentas a folha fica como antes (sem registo).
    /// </summary>
    private async Task ComposePendingRegisterAsync(BoquilhasRegistoContext context, CancellationToken cancellationToken)
    {
        if (context.Tool is not { } tool)
        {
            return;
        }

        var bqContextId = context.BqContextId;
        var toolId = context.ToolId;

        RegistoTrace = new BoquilhasRegistoTrace(
            BqRepairTraceId: 0,
            ToolId: toolId,
            ToolReference: tool.Reference,
            ToolLot: tool.Lot,
            BqContextId: bqContextId,
            Production: null,
            Movements: Array.Empty<BoquilhasRegistoMovement>(),
            QuantityLegitimatelyOut: 0,
            AccumulatedDiscrepancy: null,
            ToolAccountedQuantity: context.ToolAccountedQuantity);
        PendingRegisterBqContextId = bqContextId;

        // Sem movimentos não há nada para corrigir.
        EditMovementsFlag = null;

        var unresolved = await _db.BqRepairTraces
            .AsNoTracking()
            .Where(t => t.ToolId == toolId && t.BqContextId == null)
            .Select(t => new
            {
                t.Id,
                Movements = _db.BqMovements.Count(m => m.BqRepairTraceId == t.Id)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (unresolved is { Movements: > 0 })
        {
            PendingUnresolvedTraceId = unresolved.Id;
            PendingUnresolvedMovementCount = unresolved.Movements;
        }
    }

    /// <summary>
    /// O regresso do POST do movimento: ao registo (?trace_id) ou, na
    /// máquina ainda sem registo, à mesma máquina (?bq_id).
    /// </summary>
    private static RouteValueDictionary RegisterRoute(
        int traceId,
        int? pendingBqContextId,
        params (string Key, object? Value)[] values)
    {
        var route = new RouteValueDictionary();
        if (pendingBqContextId is int bqContextId)
        {
            route["bq_id"] = bqContextId;
        }
        else
        {
            route["trace_id"] = traceId;
        }

        foreach (var (key, value) in values)
        {
            if (value is not null)
            {
                route[key] = value;
            }
        }

        return route;
    }
}

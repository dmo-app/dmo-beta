using DMO.Alpha.Core.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// Uma boquilha encontrada ao escrever no «Procurar / trocar de boquilha ou
/// produção» (Diogo, 17/10/2026): a BQ, o lote, as máquinas em que trabalha e
/// se está em produção. <see cref="OpenHref"/> é o que o clique abre.
/// </summary>
public sealed record BqSearchResult(
    string ToolId,
    string Reference,
    string Lot,
    IReadOnlyList<string> Machines,
    string? ProductionReference,
    string? ProductionNumber,
    string? ProductionMachine,
    bool InProduction,
    string OpenHref);

public sealed partial class IndexModel
{
    /// <summary>Quantas boquilhas a procura ao escrever mostra de cada vez.</summary>
    public const int BqSearchLimit = 10;

    /// <summary>
    /// A procura ao escrever do registo (?handler=ProcurarBq&amp;q=): as BQ
    /// cuja referência, lote, produção ou referência da produção contêm o
    /// texto, no máximo <see cref="BqSearchLimit"/>. Devolve só a lista, que a
    /// página mostra por baixo do campo.
    /// </summary>
    public async Task<IActionResult> OnGetProcurarBqAsync(string? q)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var results = await SearchBqAsync(q, HttpContext.RequestAborted);
        return Partial("_BqProcura", results);
    }

    private async Task<IReadOnlyList<BqSearchResult>> SearchBqAsync(string? term, CancellationToken cancellationToken)
    {
        var wanted = term?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(wanted))
        {
            return [];
        }

        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        var tools = await _db.Tools.AsNoTracking()
            .Where(tool => tool.Type == ToolType.Bq
                && tool.ToolId != ToolRegistryConstants.RemovedToolId
                && (tool.Reference.ToUpper().Contains(wanted)
                    || tool.Lot.ToUpper().Contains(wanted)
                    || _db.BqContexts.Any(ctx => ctx.ToolId == tool.ToolId
                        && (ctx.JobOn.Reference.ToUpper().Contains(wanted)
                            || ctx.JobOn.ProductionNumber.ToUpper().Contains(wanted)))))
            .OrderBy(tool => tool.Reference).ThenBy(tool => tool.Lot)
            .Select(tool => new { tool.ToolId, tool.Reference, tool.Lot })
            .Take(BqSearchLimit)
            .ToListAsync(cancellationToken);
        var toolIds = tools.Select(tool => tool.ToolId).ToList();

        var machines = (await _db.ToolMachineLinks.AsNoTracking()
                .Where(link => toolIds.Contains(link.ToolId))
                .Select(link => new { link.ToolId, link.Machine.Code })
                .ToListAsync(cancellationToken))
            .ToLookup(link => link.ToolId, link => link.Code);

        // A produção de cada BQ: a que está em máquina hoje (pela janela do
        // Job On); senão a mais recente, só para o clique saber o que abrir.
        var contexts = (await _db.BqContexts.AsNoTracking()
                .Where(ctx => toolIds.Contains(ctx.ToolId))
                .Select(ctx => new
                {
                    ctx.Id,
                    ctx.ToolId,
                    ctx.JobOn.Reference,
                    ctx.JobOn.ProductionNumber,
                    Machine = ctx.JobOn.Machine.Code,
                    ctx.JobOn.InicioEm,
                    ctx.JobOn.FimEm
                })
                .ToListAsync(cancellationToken))
            .ToLookup(ctx => ctx.ToolId);

        // O registo mais recente de cada BQ (próprio ou pela produção).
        var latestTraces = (await _db.BqRepairTraces.AsNoTracking()
                .Where(trace => (trace.ToolId != null && toolIds.Contains(trace.ToolId))
                    || (trace.BqContext != null && toolIds.Contains(trace.BqContext.ToolId)))
                .Select(trace => new
                {
                    trace.Id,
                    ToolId = trace.BqContext != null ? trace.BqContext.ToolId : trace.ToolId
                })
                .ToListAsync(cancellationToken))
            .GroupBy(trace => trace.ToolId!)
            .ToDictionary(group => group.Key, group => group.Max(trace => trace.Id));

        return tools.Select(tool =>
        {
            var current = contexts[tool.ToolId]
                .Where(ctx => ctx.InicioEm is { } inicio && inicio <= hoje && (ctx.FimEm is not { } fim || hoje <= fim))
                .OrderByDescending(ctx => ctx.Id)
                .FirstOrDefault();
            var href = current is not null
                ? Url.Page("/Boquilhas/Index", new { bq_id = current.Id })
                : latestTraces.TryGetValue(tool.ToolId, out var traceId)
                    ? Url.Page("/Boquilhas/Index", new { trace_id = traceId })
                    : Url.Page("/Boquilhas/Index", new { ficheiros = 1, ferramenta = tool.ToolId });
            return new BqSearchResult(
                tool.ToolId,
                tool.Reference,
                tool.Lot,
                machines[tool.ToolId].OrderBy(code => code, StringComparer.Ordinal).ToList(),
                current?.Reference,
                current?.ProductionNumber,
                current?.Machine,
                current is not null,
                href!);
        }).ToList();
    }
}

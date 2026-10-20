using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// Uma boquilha encontrada ao escrever no «Procurar / trocar de boquilha ou
/// produção» (Diogo, 17/10/2026): a BQ, o lote, as máquinas em que trabalha e
/// se está em produção. <see cref="OpenHref"/> é o que o clique abre. Quando a
/// BQ apareceu por uma produção (a referência ou o nº dela, e não a BQ ou o
/// lote), <see cref="FoundIn"/> diz qual, e o clique abre o registo dela.
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
    string OpenHref,
    BqSearchProduction? FoundIn = null);

/// <summary>A produção pela qual uma boquilha foi encontrada: a referência, o nº e a máquina.</summary>
public sealed record BqSearchProduction(string? Reference, string? ProductionNumber, string? Machine);

public sealed partial class IndexModel
{
    /// <summary>Quantas boquilhas a procura ao escrever mostra de cada vez.</summary>
    public const int BqSearchLimit = 10;

    /// <summary>
    /// A procura ao escrever do registo (?handler=ProcurarBq&amp;q=): as BQ
    /// cuja referência ou a referência de uma produção dela começa pelo texto
    /// (ou a parte BQ, a regra de <see cref="ReferenceMatch"/>), ou cujo lote
    /// ou nº de uma produção contém o texto, no máximo
    /// <see cref="BqSearchLimit"/>. Devolve só a lista, que a página mostra
    /// por baixo do campo.
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

    /// <summary>
    /// As BQ que a procura encontra, pela ordem da lista: a referência da BQ
    /// pelo início (ou a parte BQ), o lote por parte do texto, ou uma produção
    /// em que a BQ foi usada, pela referência dela (a mesma regra) ou por
    /// parte do nº. Um bq_id de uma BQ trocada continua a contar pela
    /// produção. <paramref name="wanted"/> vem de <see cref="ReferenceMatch.Normalize"/>.
    /// Pública para o teste no PostgreSQL (a tradução do EF).
    /// </summary>
    public static IQueryable<Tool> BqSearchTools(DMO.Alpha.Infrastructure.Data.DmoDbContext db, string wanted)
    {
        var porProducao = db.BqContexts
            .Where(ReferenceMatch.Predicate<BqContext>(ctx => ctx.JobOn != null ? ctx.JobOn.Reference : ctx.ReplacedJobOn!.Reference, wanted)
                .Or(ctx => (ctx.JobOn != null ? ctx.JobOn.ProductionNumber : ctx.ReplacedJobOn!.ProductionNumber).ToUpper().Contains(wanted)))
            .Select(ctx => ctx.ToolId);
        return db.Tools.AsNoTracking()
            .Where(tool => tool.Type == ToolType.Bq && tool.ToolId != ToolRegistryConstants.RemovedToolId)
            .Where(ReferenceMatch.Predicate<Tool>(tool => tool.Reference, wanted)
                .Or(tool => tool.Lot.ToUpper().Contains(wanted) || porProducao.Contains(tool.ToolId)))
            .OrderBy(tool => tool.Reference).ThenBy(tool => tool.Lot);
    }

    private async Task<IReadOnlyList<BqSearchResult>> SearchBqAsync(string? term, CancellationToken cancellationToken)
    {
        if (ReferenceMatch.Normalize(term) is not { } wanted)
        {
            return [];
        }

        // «Em produção» pelo dia operacional, como o painel: de noite, antes da
        // hora de viragem, ainda é a produção de ontem (sweep de 19/10, B-1).
        var hoje = await _currentQuery.OperationalDayAsync(cancellationToken);
        var tools = await BqSearchTools(_db, wanted)
            .Select(tool => new { tool.ToolId, tool.Reference, tool.Lot })
            .Take(BqSearchLimit)
            .ToListAsync(cancellationToken);
        var toolIds = tools.Select(tool => tool.ToolId).ToList();

        var machines = (await _db.ToolMachineLinks.AsNoTracking()
                .Where(link => toolIds.Contains(link.ToolId))
                .Select(link => new { link.ToolId, link.Machine.Code })
                .ToListAsync(cancellationToken))
            .ToLookup(link => link.ToolId, link => link.Code);

        // A produção de cada BQ: a que está em máquina no dia operacional
        // (pela janela do Job On); senão a mais recente, só para o clique
        // saber o que abrir.
        var contexts = (await _db.BqContexts.AsNoTracking()
                .Where(ctx => toolIds.Contains(ctx.ToolId))
                .Select(ctx => new
                {
                    ctx.Id,
                    ctx.ToolId,
                    // Um bq_id de uma BQ trocada já não está na produção
                    // (18/10/2026): conta para a encontrar, não para «Em
                    // produção».
                    Ativo = ctx.JobOnId != null,
                    // Um bq_id de uma BQ trocada continua a mostrar a produção.
                    Reference = ctx.JobOn != null ? ctx.JobOn.Reference : ctx.ReplacedJobOn!.Reference,
                    ProductionNumber = ctx.JobOn != null ? ctx.JobOn.ProductionNumber : ctx.ReplacedJobOn!.ProductionNumber,
                    Machine = ctx.JobOn != null ? ctx.JobOn.Machine.Code : ctx.ReplacedJobOn!.Machine.Code,
                    InicioEm = ctx.JobOn != null ? ctx.JobOn.InicioEm : ctx.ReplacedJobOn!.InicioEm,
                    FimEm = ctx.JobOn != null ? ctx.JobOn.FimEm : ctx.ReplacedJobOn!.FimEm
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
                .Where(ctx => ctx.Ativo && ctx.InicioEm is { } inicio && inicio <= hoje && (ctx.FimEm is not { } fim || hoje <= fim))
                .OrderByDescending(ctx => ctx.Id)
                .FirstOrDefault();
            // Porque apareceu (deep sweep 2, F-14): sem a BQ nem o lote a
            // corresponder, foi por uma produção, a mais recente das que
            // correspondem, com a mesma regra da consulta. A pílula «Em
            // produção» continua a dizer só se está em máquina hoje.
            var foundIn = ReferenceMatch.Matches(tool.Reference, wanted)
                || tool.Lot.ToUpperInvariant().Contains(wanted, StringComparison.Ordinal)
                ? null
                : contexts[tool.ToolId]
                    .Where(ctx => ReferenceMatch.Matches(ctx.Reference, wanted)
                        || (ctx.ProductionNumber?.ToUpperInvariant().Contains(wanted, StringComparison.Ordinal) ?? false))
                    .OrderByDescending(ctx => ctx.Id)
                    .FirstOrDefault();
            var href = foundIn is not null
                ? Url.Page("/Boquilhas/Index", new { bq_id = foundIn.Id })
                : current is not null
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
                href!,
                foundIn is null ? null : new BqSearchProduction(foundIn.Reference, foundIn.ProductionNumber, foundIn.Machine));
        }).ToList();
    }
}

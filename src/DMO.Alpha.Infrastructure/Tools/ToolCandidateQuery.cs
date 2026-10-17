using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação só de leitura da descoberta de candidatos de Tool para
/// seleção humana explícita. Aplica os filtros explícitos no servidor
/// (regra de queries cirúrgicas: nunca carrega o registo completo para o
/// cliente filtrar) com projeção mínima dos factos persistidos,
/// AsNoTracking e ordenação determinística pelos FATOS HUMANOS
/// (Type → Reference → Lot; o tool_id — id opaco — é apenas o
/// desempate final da determinização, nunca a chave de leitura). A linha
/// DUMMY (REMOVED) é SEMPRE excluída por constante: a tombstone nunca é
/// candidata à seleção. Não cria Tools, não muta estado, não deriva
/// identidade de referência/lote/tipo e nunca auto-seleciona: os filtros
/// (incluindo a máquina e a referência MF associada) apenas reduzem os
/// candidatos devolvidos.
/// </summary>
public sealed class ToolCandidateQuery : IToolCandidateQuery
{
    private readonly DmoDbContext _db;

    public ToolCandidateQuery(DmoDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ToolCandidate>> FindCandidatesAsync(
        ToolCandidateFilter? filter, CancellationToken cancellationToken = default) =>
        ReadCandidatesAsync(OrderCandidates(ApplyFilters(filter)), cancellationToken);

    public async Task<ToolCandidatePage> FindPageAsync(
        ToolCandidateFilter? filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = pageSize == 20 ? 20 : 10;
        var query = ApplyFilters(filter);
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        var candidates = await ReadCandidatesAsync(OrderCandidates(query)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize), cancellationToken);
        return new ToolCandidatePage(candidates, total, pageNumber, pageSize);
    }

    private static IOrderedQueryable<Tool> OrderCandidates(IQueryable<Tool> query) => query
        .OrderBy(tool => tool.Type).ThenBy(tool => tool.Reference).ThenBy(tool => tool.Lot).ThenBy(tool => tool.ToolId);

    private IQueryable<Tool> ApplyFilters(ToolCandidateFilter? filter)
    {
        IQueryable<Tool> query = _db.Tools.AsNoTracking();

        // A DUMMY canónica nunca é candidata: excluída por CONSTANTE em
        // todas as queries de descoberta.
        query = query.Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId);

        // Dimensões fechadas e explícitas, composta por E: cada dimensão
        // apenas reduz o conjunto. null/vazio = dimensão não aplicada, sem
        // exceção de argumento (estilo do IToolLookupQuery com valores
        // desconhecidos).
        if (filter is not null)
        {
            if (filter.ToolIds is { } toolIds)
            {
                query = query.Where(tool => toolIds.Contains(tool.ToolId));
            }

            if (filter.Type is { } type)
            {
                query = query.Where(tool => tool.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(filter.Reference))
            {
                // Igualdade exata ao valor persistido: sem trimming, sem
                // case-insensitivity, sem contains — nenhuma semântica
                // difusa inventada.
                query = query.Where(tool => tool.Reference == filter.Reference);
            }

            if (!string.IsNullOrWhiteSpace(filter.Lot))
            {
                query = query.Where(tool => tool.Lot == filter.Lot);
            }

            if (filter.MachineId is { } machineId)
            {
                // Compatibilidade de máquina (via ligações persistidas):
                // reduz candidatos, nunca determina identidade.
                query = query.Where(tool =>
                    _db.ToolMachineLinks.Any(link =>
                        link.ToolId == tool.ToolId && link.MachineId == machineId));
            }

            if (!string.IsNullOrWhiteSpace(filter.MfReference))
            {
                // Referência MF/produção (worked example blueprint L70-93):
                // corresponde a Reference == valor OU à associação explícita
                // AssociatedMfReference == valor, QUALQUER tipo — a CM cuja
                // referência canónica difere da referência MF em que encaixa
                // continua candidata pela associação. A seleção permanece
                // humana explícita sobre o tool_id.
                query = query.Where(tool =>
                    tool.Reference == filter.MfReference
                    || tool.AssociatedMfReference == filter.MfReference);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var search = filter.Search.Trim().ToUpperInvariant();
            query = query.Where(tool => tool.Reference.ToUpper().Contains(search)
                || tool.Lot.ToUpper().Contains(search)
                || _db.ToolMachineLinks.Any(link => link.ToolId == tool.ToolId
                    && link.Machine.Code.ToUpper().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter?.ProductionReference))
        {
            var productionReference = filter.ProductionReference.Trim().ToUpperInvariant();
            var usedToolIds = _db.CmContexts.Where(c => c.JobOn.Reference.ToUpper() == productionReference).Select(c => c.ToolId)
                .Concat(_db.MfContexts.Where(c => c.JobOn.Reference.ToUpper() == productionReference).Select(c => c.ToolId))
                .Concat(_db.BqContexts.Where(c => c.JobOn.Reference.ToUpper() == productionReference).Select(c => c.ToolId));
            var knownTools = _db.Tools.Where(known => usedToolIds.Contains(known.ToolId) && known.ToolId != ToolRegistryConstants.RemovedToolId);
            query = query.Where(tool =>
                tool.Reference.ToUpper() == productionReference
                || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == productionReference)
                // Replacement lots of the same recorded design remain discoverable.
                || knownTools.Any(known => known.Type == tool.Type && known.Reference.ToUpper() == tool.Reference.ToUpper()));
        }

        return query;
    }

    private async Task<IReadOnlyList<ToolCandidate>> ReadCandidatesAsync(
        IQueryable<Tool> query, CancellationToken cancellationToken)
    {
        // A QUANTIDADE e as MÁQUINAS (o Owner 2026-10-12: a escolha não
        // fica «tão bare»): a quantidade é coluna direta da Tool; as
        // máquinas do link n-n materializam-se em memória (o
        // string.Join é pós-materialização — o EF/Npgsql não o traduz;
        // o padrão agnóstico InMemory/Npgsql do bloco).
        var rows = await query
            .Select(tool => new
            {
                tool.ToolId,
                tool.Type,
                tool.Reference,
                tool.Lot,
                tool.BqAccountedQuantity,
                Machines = _db.ToolMachineLinks
                    .Where(link => link.ToolId == tool.ToolId)
                    .Select(link => link.Machine.Code)
                    .OrderBy(code => code)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ToolCandidate(
                row.ToolId,
                row.Type,
                row.Reference,
                row.Lot,
                row.BqAccountedQuantity,
                row.Machines.Count > 0 ? string.Join(" · ", row.Machines) : null))
            .ToList();
    }
}

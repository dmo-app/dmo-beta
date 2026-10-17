using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação só de leitura do surface nativo de resolução de uma Tool
/// pelo seu tool_id canónico exato, para o fluxo do Registo. A correspondência
/// é feita exclusivamente pelo ToolId (nunca por referência, lote ou tipo); a
/// projeção é mínima e usa AsNoTracking, sem criar Tools, sem mutar estado e
/// sem carregar dados não relacionados. Devolve null quando o tool_id é
/// desconhecido — e SEMPRE null para a linha DUMMY canónica (REMOVED),
/// excluída por constante: a tombstone não é utilizável como Tool real em
/// nenhum fluxo. O Tool.Type nunca é um gate.
/// </summary>
public sealed class ToolLookupQuery : IToolLookupQuery
{
    private readonly DmoDbContext _db;

    public ToolLookupQuery(DmoDbContext db)
    {
        _db = db;
    }

    public Task<ToolLookupResult?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default)
    {
        return _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new ToolLookupResult(t.ToolId, t.Type, t.Reference, t.Lot))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ToolLookupResult>> FindByToolIdsAsync(
        IEnumerable<string?> toolIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = toolIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<string, ToolLookupResult>();
        }

        return await _db.Tools
            .AsNoTracking()
            .Where(t => wanted.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new ToolLookupResult(t.ToolId, t.Type, t.Reference, t.Lot))
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
    }

    public async Task<ToolLookupResult?> FindByReferenceAsync(
        string reference,
        ToolType type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var wanted = reference.Trim().ToUpperInvariant();
        return await _db.Tools
            .AsNoTracking()
            .Where(t => t.Type == type
                && t.Reference != null
                && t.Reference.ToUpper() == wanted
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .OrderByDescending(t => t.Lot.Length)
            .ThenByDescending(t => t.Lot)
            .ThenByDescending(t => t.ToolId)
            .Select(t => new ToolLookupResult(t.ToolId, t.Type, t.Reference, t.Lot))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

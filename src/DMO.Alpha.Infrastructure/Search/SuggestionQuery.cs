using DMO.Alpha.Core.Search;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Search;

/// <summary>
/// As sugestões ao escrever: uma consulta curta por pedido, só as colunas
/// mostradas, filtro no servidor sem distinção de maiúsculas (a mesma regra
/// da procura do Resumo, H22-02) e corte em <see cref="ISuggestionQuery.MaxResults"/>.
/// Abaixo de <see cref="ISuggestionQuery.MinimumLength"/> caracteres não
/// consulta a base de dados.
/// </summary>
public sealed class SuggestionQuery : ISuggestionQuery
{
    private const int Max = ISuggestionQuery.MaxResults;
    private readonly DmoDbContext _db;

    public SuggestionQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Suggestion>> SuggestAsync(
        SuggestionSource source,
        string? term,
        ToolType? toolType = null,
        CancellationToken cancellationToken = default)
    {
        var wanted = term?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(wanted) || wanted.Length < ISuggestionQuery.MinimumLength)
        {
            return Array.Empty<Suggestion>();
        }

        return source switch
        {
            SuggestionSource.Producoes => await ProducoesAsync(wanted, byReference: true, byNumber: true, cancellationToken),
            SuggestionSource.NumerosProducao => await ProducoesAsync(wanted, byReference: false, byNumber: true, cancellationToken),
            SuggestionSource.Referencias => await ReferenciasAsync(wanted, cancellationToken),
            SuggestionSource.Ferramentas => await FerramentasAsync(wanted, toolType, cancellationToken),
            SuggestionSource.Reparadores => await ReparadoresAsync(wanted, cancellationToken),
            _ => Array.Empty<Suggestion>()
        };
    }

    private async Task<IReadOnlyList<Suggestion>> ProducoesAsync(
        string wanted, bool byReference, bool byNumber, CancellationToken cancellationToken)
    {
        var rows = await (
            from j in _db.JobOns.AsNoTracking()
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            where (byReference && j.Reference.ToUpper().Contains(wanted))
                || (byNumber && j.ProductionNumber.ToUpper().Contains(wanted))
            orderby j.Id descending
            select new { j.Reference, j.ProductionNumber, Machine = m.Code })
            .Take(Max)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new Suggestion(
                byReference && r.Reference.ToUpperInvariant().Contains(wanted, StringComparison.Ordinal)
                    ? r.Reference
                    : r.ProductionNumber,
                $"{r.Reference} · {r.ProductionNumber} · {r.Machine}"))
            .ToList();
    }

    private async Task<IReadOnlyList<Suggestion>> ReferenciasAsync(
        string wanted, CancellationToken cancellationToken)
    {
        var references = await _db.JobOns.AsNoTracking()
            .Where(j => j.Reference.ToUpper().Contains(wanted))
            .Select(j => j.Reference)
            .Distinct()
            .OrderBy(reference => reference)
            .Take(Max)
            .ToListAsync(cancellationToken);

        return references.Select(reference => new Suggestion(reference, reference)).ToList();
    }

    private async Task<IReadOnlyList<Suggestion>> FerramentasAsync(
        string wanted, ToolType? toolType, CancellationToken cancellationToken)
    {
        var query = _db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId
                && tool.Reference.ToUpper().Contains(wanted));
        if (toolType is { } type)
        {
            query = query.Where(tool => tool.Type == type);
        }

        var rows = await query
            .Select(tool => new { tool.Type, tool.Reference })
            .Distinct()
            .OrderBy(row => row.Reference).ThenBy(row => row.Type)
            .Take(Max)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new Suggestion(row.Reference, $"{row.Type.ToString().ToUpperInvariant()} {row.Reference}"))
            .ToList();
    }

    private async Task<IReadOnlyList<Suggestion>> ReparadoresAsync(
        string wanted, CancellationToken cancellationToken)
    {
        var names = await _db.BqRepairers.AsNoTracking()
            .Where(repairer => repairer.Name.ToUpper().Contains(wanted))
            .OrderBy(repairer => repairer.Name)
            .Select(repairer => repairer.Name)
            .Take(Max)
            .ToListAsync(cancellationToken);

        return names.Select(name => new Suggestion(name, name)).ToList();
    }
}

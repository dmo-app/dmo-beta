using System.Linq.Expressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Search;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Search;

/// <summary>
/// As sugestões ao escrever: uma consulta curta por pedido, só as colunas
/// mostradas, filtro no servidor sem distinção de maiúsculas e corte em
/// <see cref="ISuggestionQuery.MaxResults"/>. As referências seguem a regra
/// de <see cref="ReferenceMatch"/> (o início da referência ou da parte BQ,
/// nunca a meio) e os nºs de produção o início do nº; os reparadores
/// encontram-se por parte do nome. Abaixo do mínimo de caracteres da fonte
/// não consulta a base de dados.
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
        var minimum = source == SuggestionSource.Reparadores
            ? ISuggestionQuery.NameMinimumLength
            : ISuggestionQuery.MinimumLength;
        if (ReferenceMatch.Normalize(term) is not { } wanted || wanted.Length < minimum)
        {
            return Array.Empty<Suggestion>();
        }

        return source switch
        {
            SuggestionSource.Producoes => await ProducoesAsync(wanted, byReference: true, cancellationToken),
            SuggestionSource.NumerosProducao => await ProducoesAsync(wanted, byReference: false, cancellationToken),
            SuggestionSource.Referencias => await ReferenciasAsync(wanted, cancellationToken),
            SuggestionSource.Ferramentas => await FerramentasAsync(wanted, toolType, cancellationToken),
            SuggestionSource.Reparadores => await ReparadoresAsync(wanted, cancellationToken),
            SuggestionSource.ReferenciasEBq => await ReferenciasEBqAsync(wanted, cancellationToken),
            SuggestionSource.ReferenciasRegisto => await ReferenciasRegistoAsync(wanted, toolType, cancellationToken),
            _ => Array.Empty<Suggestion>()
        };
    }

    private async Task<IReadOnlyList<Suggestion>> ProducoesAsync(
        string wanted, bool byReference, CancellationToken cancellationToken)
    {
        // O nº pelo início: com um carácter, «0» a meio dos nºs (2026001)
        // enchia a lista e a 0132T173 não aparecia.
        Expression<Func<JobOn, bool>> byNumber = j => j.ProductionNumber.ToUpper().StartsWith(wanted);
        var jobs = _db.JobOns.AsNoTracking()
            .Where(byReference ? ReferenceMatch.Predicate<JobOn>(j => j.Reference, wanted).Or(byNumber) : byNumber);
        var rows = await (
            from j in jobs
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            orderby j.Id descending
            select new { j.Reference, j.ProductionNumber, Machine = m.Code })
            .Take(Max)
            .ToListAsync(cancellationToken);

        // A mesma regra em memória decide se o valor é a referência ou o nº
        // (um nº que começa pelo texto nunca devolve a referência).
        return rows
            .Select(r => new Suggestion(
                byReference && ReferenceMatch.Matches(r.Reference, wanted)
                    ? r.Reference
                    : r.ProductionNumber,
                $"{r.Reference} · {r.ProductionNumber} · {r.Machine}"))
            .ToList();
    }

    private Task<List<string>> ProductionReferencesAsync(string wanted, CancellationToken cancellationToken) =>
        _db.JobOns.AsNoTracking()
            .Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, wanted))
            .Select(j => j.Reference)
            .Distinct()
            .OrderBy(reference => reference)
            .Take(Max)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<Suggestion>> ReferenciasAsync(
        string wanted, CancellationToken cancellationToken)
    {
        var references = await ProductionReferencesAsync(wanted, cancellationToken);
        return references.Select(reference => new Suggestion(reference, reference)).ToList();
    }

    private IQueryable<Tool> ToolsMatching(string wanted, ToolType? toolType)
    {
        var query = _db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId)
            .Where(ReferenceMatch.Predicate<Tool>(tool => tool.Reference, wanted));
        if (toolType is { } type)
        {
            query = query.Where(tool => tool.Type == type);
        }
        return query;
    }

    private async Task<IReadOnlyList<Suggestion>> FerramentasAsync(
        string wanted, ToolType? toolType, CancellationToken cancellationToken)
    {
        var rows = await ToolsMatching(wanted, toolType)
            .Select(tool => new { tool.Type, tool.Reference })
            .Distinct()
            .OrderBy(row => row.Reference).ThenBy(row => row.Type)
            .Take(Max)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new Suggestion(row.Reference, $"{row.Type.ToString().ToUpperInvariant()} {row.Reference}"))
            .ToList();
    }

    /// <summary>
    /// As referências de produção e as de BQ (deep sweep 2, F-15: na pesquisa
    /// do Histórico das Boquilhas, «3540» dava «Sem sugestões» e o filtro
    /// encontrava a referência). Primeiro as de produção, depois as de BQ,
    /// marcadas «BQ»; quando há muitas das duas, metade de cada.
    /// </summary>
    private async Task<IReadOnlyList<Suggestion>> ReferenciasEBqAsync(
        string wanted, CancellationToken cancellationToken)
    {
        var references = await ProductionReferencesAsync(wanted, cancellationToken);
        var bqs = await ToolsMatching(wanted, ToolType.Bq)
            .Select(tool => tool.Reference)
            .Distinct()
            .OrderBy(reference => reference)
            .Take(Max)
            .ToListAsync(cancellationToken);

        var bqTake = Math.Min(bqs.Count, Math.Max(Max / 2, Max - references.Count));
        var referenceTake = Math.Min(references.Count, Max - bqTake);
        return references.Take(referenceTake).Select(reference => new Suggestion(reference, reference))
            .Concat(bqs.Take(bqTake).Select(reference => new Suggestion(reference, $"BQ {reference}")))
            .ToList();
    }

    /// <summary>
    /// O «Registar ferramenta real» (Diogo, 19/10/2026): a referência fica
    /// sempre toda («deveria ser sempre referência toda»; «C142» e «1075»
    /// mostram a 1075C142). Primeiro as ferramentas do tipo que já existem,
    /// pela referência ou, na CM, pela referência MF associada, e com o nome
    /// original (uma ST100 da 1075C142 aparece e escreve ST100); depois as
    /// referências completas das produções. O mesmo valor aparece uma vez
    /// só; quando há muitas das duas, metade de cada.
    /// </summary>
    private async Task<IReadOnlyList<Suggestion>> ReferenciasRegistoAsync(
        string wanted, ToolType? toolType, CancellationToken cancellationToken)
    {
        var query = _db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId)
            .Where(ReferenceMatch.Predicate<Tool>(tool => tool.Reference, wanted)
                .Or(ReferenceMatch.Predicate<Tool>(tool => tool.Type == ToolType.Cm ? tool.AssociatedMfReference : null, wanted)));
        if (toolType is { } type)
        {
            query = query.Where(tool => tool.Type == type);
        }
        var rows = await query
            .Select(tool => new { tool.Reference, tool.AssociatedMfReference })
            .Distinct()
            .OrderBy(row => row.Reference)
            .Take(Max)
            .ToListAsync(cancellationToken);
        var tools = rows
            .DistinctBy(row => row.Reference)
            .Select(row => new Suggestion(row.Reference,
                row.AssociatedMfReference is { Length: > 0 } mf && mf != row.Reference ? $"{row.Reference} · {mf}" : row.Reference))
            .ToList();
        var references = await ProductionReferencesAsync(wanted, cancellationToken);
        var fromProductions = references
            .Where(reference => tools.All(tool => tool.Value != reference))
            .Select(reference => new Suggestion(reference, reference))
            .ToList();

        var toolTake = Math.Min(tools.Count, Math.Max(Max / 2, Max - fromProductions.Count));
        return tools.Take(toolTake).Concat(fromProductions.Take(Max - toolTake)).ToList();
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

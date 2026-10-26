using System.Linq.Expressions;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// A regra única da referência total (MF + BQ: 1075C142) que liga as
/// ferramentas (Diogo, 26/10/2026: «ST100 faz parte do 1075, 1075 usa C142»).
/// A CM liga-se ao MF pela «Ref. MF associada» e o MF à BQ pelas referências
/// totais que existem (as dos Job On e as gravadas nas ferramentas). Usada
/// pela procura das Ferramentas, pelo Job On e pelas sugestões do Peso e dos
/// Pegamentos sem Job On.
/// </summary>
public static class ReferenciaTotal
{
    /// <summary>
    /// A ferramenta pertence à referência total: a referência toda, a parte
    /// BQ (só a BQ) ou o modelo (a CM e o MF, também pela referência MF
    /// associada). As registadas só com uma parte também pertencem.
    /// </summary>
    public static Expression<Func<Tool, bool>> Pertence(string referenciaTotal)
    {
        var total = referenciaTotal.Trim().ToUpperInvariant();
        var (model, bqPart) = ToolCandidateQuery.SplitReference(total);
        return tool =>
            tool.Reference.ToUpper() == total
            || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == total)
            || (bqPart != null && tool.Type == ToolType.Bq && tool.Reference.ToUpper() == bqPart)
            || (model != null && tool.Type != ToolType.Bq && (tool.Reference.ToUpper() == model
                || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == model)));
    }

    /// <summary>A ferramenta pertence a alguma destas referências totais; nenhuma, a nenhuma.</summary>
    public static Expression<Func<Tool, bool>> PertenceAlguma(IReadOnlyCollection<string> referenciasTotais)
    {
        Expression<Func<Tool, bool>>? todas = null;
        foreach (var total in referenciasTotais)
        {
            todas = todas is null ? Pertence(total) : todas.Or(Pertence(total));
        }
        return todas ?? (tool => false);
    }

    /// <summary>
    /// As referências totais conhecidas em que entra a ferramenta com esta
    /// referência: ela própria, se já é total; as do modelo dela ou da
    /// referência MF associada (a CM ST100 → 1075 → 1075C142); as da parte
    /// BQ (C142 → 1075C142). Vazio quando nada a liga.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DaFerramentaAsync(
        DmoDbContext db, string referencia, CancellationToken cancellationToken = default)
    {
        var wanted = referencia.Trim().ToUpperInvariant();
        if (wanted.Length == 0) return Array.Empty<string>();

        var associadas = await db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId
                && tool.Reference.ToUpper() == wanted && tool.AssociatedMfReference != null)
            .Select(tool => tool.AssociatedMfReference!)
            .Distinct()
            .ToListAsync(cancellationToken);
        var modelos = new HashSet<string>(StringComparer.Ordinal) { wanted };
        var totaisDiretos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var associada in associadas.Select(a => a.Trim().ToUpperInvariant()))
        {
            if (ToolCandidateQuery.SplitReference(associada) is ({ } model, not null))
            {
                totaisDiretos.Add(associada);
                modelos.Add(model);
            }
            else if (associada.Length > 0)
            {
                modelos.Add(associada);
            }
        }

        var conhecidas = await db.JobOns.AsNoTracking().Select(j => j.Reference)
            .Concat(db.Tools.AsNoTracking().Select(t => t.Reference))
            .Concat(db.Tools.AsNoTracking().Where(t => t.AssociatedMfReference != null).Select(t => t.AssociatedMfReference!))
            .Distinct()
            .ToListAsync(cancellationToken);

        return conhecidas
            .Select(reference => reference.Trim().ToUpperInvariant())
            .Where(reference => ToolCandidateQuery.SplitReference(reference) is ({ } model, { } bq)
                && (reference == wanted || totaisDiretos.Contains(reference) || modelos.Contains(model) || bq == wanted))
            .Distinct()
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
    }
}

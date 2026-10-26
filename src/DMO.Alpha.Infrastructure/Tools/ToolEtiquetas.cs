using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// A referência e o lote das ferramentas, só para a etiqueta de um cartão
/// («CM 9262 · Lote 16» no Estado do documento do Resumo). Leitura mínima
/// e sem seguimento, à parte da resolução de Tools do Registo.
/// </summary>
public sealed class ToolEtiquetas
{
    private readonly DmoDbContext _db;

    public ToolEtiquetas(DmoDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, string>> GetAsync(
        IEnumerable<string?> toolIds,
        CancellationToken cancellationToken = default)
    {
        var ids = toolIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var rows = await _db.Tools.AsNoTracking()
            .Where(t => ids.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.Reference, t.Lot })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.ToolId, r => $"{r.Reference} · Lote {r.Lot}");
    }
}

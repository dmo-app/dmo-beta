using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação só de leitura do contexto técnico Tool-owned: resolução
/// cirúrgica por tool_id canónico exato com projeção mínima e AsNoTracking.
/// Não cria Tools, não muta valores técnicos, não deriva identidade de
/// referência/lote/tipo e devolve null quando o tool_id não existe ou a Tool
/// ainda não tem linha de contexto técnico registada. A projeção inclui
/// TODAS as colunas técnicas persistidas (processo, pesos/volumes/gargalo e
/// a geometria TP de referência — a app NUNCA recalcula os derivados);
/// cada consumidor usa as que lhe dizem respeito (a página Peso só lê o
/// record — zero quebras).
/// </summary>
public sealed class ToolTechnicalQuery : IToolTechnicalQuery
{
    private readonly DmoDbContext _db;

    public ToolTechnicalQuery(DmoDbContext db)
    {
        _db = db;
    }

    public Task<ToolTechnicalReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return Task.FromResult<ToolTechnicalReadModel?>(null);
        }

        return _db.ToolTechnicalContexts
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new ToolTechnicalReadModel(
                t.ToolId,
                t.Process,
                t.NominalWeightGrams,
                t.VolumePuncaoCm3,
                t.VolumeMarisaCm3,
                t.DiametroGargaloMm,
                t.TpDiametroMm,
                t.TpRaioMm,
                t.TpSagittaMm,
                t.TpRaioBaseMm,
                t.TpRaioEsferaMm,
                t.TpVolumeMm3))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

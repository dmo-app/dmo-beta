using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// A Reparação do Armazém sobre o <see cref="DmoDbContext"/>. «Fora, em
/// reparação» é cada CM ou MF cujo último movimento (o maior id, a mesma
/// regra da localização) é uma Saída para Reparação. Só CM e MF: a
/// reparação das Boquilhas segue-se no módulo Boquilhas (Diogo, 22/10/2026).
/// </summary>
public sealed class ArmazemReparacao(DmoDbContext db) : IArmazemReparacao
{
    public async Task<IReadOnlyList<ArmazemForaEmReparacao>> ForaAsync(CancellationToken cancellationToken = default)
    {
        var ultimos = db.ArmazemMovimentos
            .GroupBy(m => m.ToolId)
            .Select(g => g.Max(m => m.Id));
        var linhas = await (
            from m in db.ArmazemMovimentos.AsNoTracking()
            join t in db.Tools.AsNoTracking() on m.ToolId equals t.ToolId
            where ultimos.Contains(m.Id)
                && m.Tipo == ArmazemMovimentoTipo.Saida && m.Destino == ArmazemDestino.Reparacao
                && t.ToolId != ToolRegistryConstants.RemovedToolId
                && (t.Type == ToolType.Cm || t.Type == ToolType.Mf)
            orderby m.OcorridoEmUtc descending, m.Id descending
            select new ArmazemForaEmReparacao(t.ToolId, t.Type, t.Reference, t.Lot, m.ReparadorNome, m.OcorridoEmUtc))
            .ToListAsync(cancellationToken);
        return linhas;
    }
}

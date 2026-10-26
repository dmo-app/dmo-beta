using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// Os próximos Job On sobre o <see cref="DmoDbContext"/>: as produções com
/// início entre hoje e daqui a uns dias, pela data de início, com a CM, o MF e
/// a BQ em uso (os contextos ainda ligados à produção) e onde está cada uma
/// (a ocupação ativa ou a localização do Armazém; sem nada, o Fabrico).
/// </summary>
public sealed class ArmazemProximos(DmoDbContext db) : IArmazemProximos
{
    public async Task<IReadOnlyList<ArmazemProximoJobOn>> ListarAsync(DateOnly hoje, int dias, CancellationToken cancellationToken = default)
    {
        var ate = hoje.AddDays(Math.Max(0, dias));
        var jobOns = await db.JobOns.AsNoTracking()
            .Where(j => j.InicioEm != null && j.InicioEm >= hoje && j.InicioEm <= ate)
            .OrderBy(j => j.InicioEm).ThenBy(j => j.Machine.Code).ThenBy(j => j.Id)
            .Select(j => new { j.Id, InicioEm = j.InicioEm!.Value, Maquina = j.Machine.Code, j.Reference, j.ProductionNumber })
            .ToListAsync(cancellationToken);
        if (jobOns.Count == 0)
        {
            return [];
        }

        var ids = jobOns.Select(j => j.Id).ToList();
        var usos = (await db.CmContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
                .Select(c => new { JobOnId = c.JobOnId!.Value, c.ToolId }).ToListAsync(cancellationToken))
            .Concat(await db.MfContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
                .Select(c => new { JobOnId = c.JobOnId!.Value, c.ToolId }).ToListAsync(cancellationToken))
            .Concat(await db.BqContexts.AsNoTracking().Where(c => c.JobOnId != null && ids.Contains(c.JobOnId.Value))
                .Select(c => new { JobOnId = c.JobOnId!.Value, c.ToolId }).ToListAsync(cancellationToken))
            .ToList();
        var toolIds = usos.Select(u => u.ToolId).Distinct().ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId))
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
        var posicoes = (await (
                from o in db.ArmazemOcupacoes.AsNoTracking()
                join p in db.ArmazemPosicoes.AsNoTracking() on o.PosicaoId equals p.Id
                where o.LibertadaEmUtc == null && toolIds.Contains(o.ToolId)
                orderby o.DesdeUtc, o.Id
                select new { o.ToolId, p.Codigo })
            .ToListAsync(cancellationToken))
            .GroupBy(o => o.ToolId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(o => o.Codigo)));
        var localizacoes = await db.FerramentaLocalizacoes.AsNoTracking()
            .Where(l => toolIds.Contains(l.ToolId))
            .ToDictionaryAsync(l => l.ToolId, cancellationToken);

        ArmazemProximaFerramenta? Ferramenta(string toolId)
        {
            if (!tools.TryGetValue(toolId, out var tool))
            {
                return null;
            }

            if (posicoes.TryGetValue(toolId, out var codigo))
            {
                return new(toolId, tool.Type, tool.Reference, tool.Lot, $"Armazém · posição {codigo}", ArmazemProximoAcao.Tirar);
            }

            var onde = localizacoes.TryGetValue(toolId, out var l) ? l.Localizacao : FerramentaLocalizacaoTipo.Fabrico;
            return new(toolId, tool.Type, tool.Reference, tool.Lot,
                ArmazemTextos.Localizacao(onde, l?.ReparadorNome),
                onde == FerramentaLocalizacaoTipo.Fabrico ? ArmazemProximoAcao.Pronta : ArmazemProximoAcao.Indisponivel);
        }

        return jobOns.Select(j => new ArmazemProximoJobOn(
                j.Id, j.InicioEm, j.Maquina, j.Reference, j.ProductionNumber,
                usos.Where(u => u.JobOnId == j.Id)
                    .Select(u => Ferramenta(u.ToolId))
                    .OfType<ArmazemProximaFerramenta>()
                    .OrderBy(f => f.Tipo == Core.Tools.ToolType.Cm ? 0 : f.Tipo == Core.Tools.ToolType.Mf ? 1 : 2)
                    .ToList()))
            .ToList();
    }
}

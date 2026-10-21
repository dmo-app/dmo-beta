using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// O mapa do Armazém sobre o <see cref="DmoDbContext"/>: lê as posições, as
/// ocupações ativas e o último estado de cada ferramenta, e agrupa por armário
/// (<see cref="ArmazemArmarios.De"/>). Uma posição arquivada só conta enquanto
/// tiver ferramentas (é preciso tirá-las de lá).
/// </summary>
public sealed class ArmazemMapa(DmoDbContext db) : IArmazemMapa
{
    public async Task<ArmazemMapaModelo> MapaAsync(string? armario, ArmazemMapaFiltro filtro, CancellationToken cancellationToken = default)
    {
        var posicoes = await db.ArmazemPosicoes.AsNoTracking()
            .Select(p => new { p.Id, p.Codigo, p.Ativa })
            .ToListAsync(cancellationToken);
        var ocupacoes = await (
                from o in db.ArmazemOcupacoes.AsNoTracking()
                join t in db.Tools.AsNoTracking() on o.ToolId equals t.ToolId
                where o.LibertadaEmUtc == null
                select new { o.PosicaoId, o.ToolId, t.Type, t.Reference, t.Lot, o.DesdeUtc })
            .ToListAsync(cancellationToken);
        var ids = ocupacoes.Select(o => o.ToolId).Distinct().ToList();
        var estados = (await db.FerramentaEstados.AsNoTracking()
                .Where(e => ids.Contains(e.ToolId))
                .Select(e => new { e.ToolId, e.Id, e.Estado })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(e => e.Id)!.Estado);

        var porPosicao = ocupacoes
            .GroupBy(o => o.PosicaoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ArmazemMapaFerramenta>)g
                    .OrderBy(o => o.Type).ThenBy(o => o.Reference).ThenBy(o => o.Lot)
                    .Select(o => new ArmazemMapaFerramenta(
                        o.ToolId, o.Type, o.Reference, o.Lot, o.DesdeUtc,
                        estados.TryGetValue(o.ToolId, out var estado) ? estado : null))
                    .ToList());

        var todas = posicoes
            .Select(p => new ArmazemMapaPosicao(
                p.Id, p.Codigo, p.Ativa,
                porPosicao.TryGetValue(p.Id, out var ferramentas) ? ferramentas : []))
            .Where(p => p.Ativa || !p.Livre)
            .OrderBy(p => p.Codigo, StringComparer.Ordinal)
            .ToList();

        var armarios = todas
            .GroupBy(p => ArmazemArmarios.De(p.Codigo))
            .Select(g => new ArmazemArmario(g.Key, g.Count(), g.Count(p => !p.Livre)))
            .OrderBy(a => a.Chave, StringComparer.Ordinal)
            .ToList();

        var escolhido = armario == ArmazemArmarios.Todos
            ? ArmazemArmarios.Todos
            : armarios.FirstOrDefault(a => a.Chave == armario)?.Chave ?? armarios.FirstOrDefault()?.Chave;
        var doArmario = escolhido == ArmazemArmarios.Todos
            ? todas
            : todas.Where(p => ArmazemArmarios.De(p.Codigo) == escolhido).ToList();

        var lista = filtro switch
        {
            ArmazemMapaFiltro.Livres => doArmario.Where(p => p.Livre).ToList(),
            ArmazemMapaFiltro.Ocupadas => doArmario.Where(p => !p.Livre).ToList(),
            ArmazemMapaFiltro.PorReparar => doArmario.Where(p => p.PorReparar).ToList(),
            _ => doArmario
        };

        return new ArmazemMapaModelo(armarios, escolhido, doArmario.Count, doArmario.Count(p => !p.Livre), lista);
    }
}

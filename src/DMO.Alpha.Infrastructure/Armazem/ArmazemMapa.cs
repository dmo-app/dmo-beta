using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// O mapa do Armazém sobre o <see cref="DmoDbContext"/>: lê as posições, as
/// ocupações ativas e o último estado de cada ferramenta, e agrupa por estante
/// (<see cref="ArmazemArmarios.De"/>) e por corredor. Uma posição arquivada só conta enquanto
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
        // O último estado (maior Id) de cada ferramenta, escolhido na base: uma
        // pesquisa pelo índice (ToolId, Id) por ferramenta, sem ler a história.
        var estados = (await db.Tools.AsNoTracking()
                .Where(t => ids.Contains(t.ToolId))
                .Select(t => new
                {
                    t.ToolId,
                    Ultimo = db.FerramentaEstados
                        .Where(e => e.ToolId == t.ToolId)
                        .OrderByDescending(e => e.Id)
                        .Select(e => (FerramentaEstadoTipo?)e.Estado)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken))
            .Where(x => x.Ultimo != null)
            .ToDictionary(x => x.ToolId, x => x.Ultimo!.Value);

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

        // A mesma ferramenta em mais de uma posição (Diogo, 26/10/2026): as posições onde isso acontece.
        var emVarias = ocupacoes.GroupBy(o => o.ToolId).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var todas = posicoes
            .Select(p => new ArmazemMapaPosicao(
                p.Id, p.Codigo, p.Ativa,
                porPosicao.TryGetValue(p.Id, out var ferramentas) ? ferramentas : [],
                porPosicao.TryGetValue(p.Id, out var comFerramentas) && comFerramentas.Any(f => emVarias.Contains(f.ToolId))))
            .Where(p => p.Ativa || !p.Livre)
            .OrderBy(p => p.Codigo, StringComparer.Ordinal)
            .ToList();

        var armarios = todas
            .GroupBy(p => ArmazemArmarios.De(p.Codigo))
            .Select(g => new ArmazemArmario(g.Key, g.Count(), g.Count(p => !p.Livre)))
            .OrderBy(a => a.Chave, StringComparer.Ordinal)
            .ToList();

        // Uma estante («103»), um corredor inteiro («1») ou todas («*»).
        var escolhido = armario == ArmazemArmarios.Todos
            ? ArmazemArmarios.Todos
            : armarios.FirstOrDefault(a => a.Chave == armario)?.Chave
                ?? (armario is { Length: 1 } && armarios.Any(a => a.Corredor == armario) ? armario : null)
                ?? armarios.FirstOrDefault()?.Chave;
        var doArmario = escolhido == ArmazemArmarios.Todos
            ? todas
            : escolhido is { Length: 1 }
                ? todas.Where(p => ArmazemArmarios.Corredor(p.Codigo) == escolhido).ToList()
                : todas.Where(p => ArmazemArmarios.De(p.Codigo) == escolhido).ToList();

        var lista = filtro switch
        {
            ArmazemMapaFiltro.Livres => doArmario.Where(p => p.Livre).ToList(),
            ArmazemMapaFiltro.Ocupadas => doArmario.Where(p => !p.Livre).ToList(),
            ArmazemMapaFiltro.PorReparar => doArmario.Where(p => p.PorReparar).ToList(),
            ArmazemMapaFiltro.Duplicadas => doArmario.Where(p => p.EmDuplicado).ToList(),
            _ => doArmario
        };

        return new ArmazemMapaModelo(armarios, escolhido, doArmario.Count, doArmario.Count(p => !p.Livre), lista);
    }
}

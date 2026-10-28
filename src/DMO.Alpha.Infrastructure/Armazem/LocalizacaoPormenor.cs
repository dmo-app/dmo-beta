using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// O sítio escolhido na Saída, para o texto da localização (Diogo,
/// 28/10/2026): o reparador ou, no empréstimo, a fábrica gravada no
/// movimento da Saída.
/// </summary>
internal static class LocalizacaoPormenor
{
    public static async Task<IReadOnlyDictionary<string, string?>> PorFerramentaAsync(
        DmoDbContext db, IEnumerable<FerramentaLocalizacao> localizacoes, CancellationToken cancellationToken)
    {
        var lista = localizacoes.ToList();
        var movimentos = lista
            .Where(l => l.Localizacao == FerramentaLocalizacaoTipo.Emprestado && string.IsNullOrWhiteSpace(l.ReparadorNome))
            .Select(l => l.MovimentoId)
            .Distinct()
            .ToList();
        var fabricas = movimentos.Count == 0
            ? new Dictionary<int, string?>()
            : await db.ArmazemMovimentos.AsNoTracking()
                .Where(m => movimentos.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.FabricaDestino, cancellationToken);
        return lista.ToDictionary(
            l => l.ToolId,
            l => string.IsNullOrWhiteSpace(l.ReparadorNome) ? fabricas.GetValueOrDefault(l.MovimentoId) : l.ReparadorNome,
            StringComparer.Ordinal);
    }

    public static async Task<string?> DeAsync(DmoDbContext db, FerramentaLocalizacao? localizacao, CancellationToken cancellationToken) =>
        localizacao is null
            ? null
            : (await PorFerramentaAsync(db, [localizacao], cancellationToken))[localizacao.ToolId];
}

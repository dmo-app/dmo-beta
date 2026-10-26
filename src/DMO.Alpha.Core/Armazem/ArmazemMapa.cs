using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>O que o mapa mostra de cada estante.</summary>
public enum ArmazemMapaFiltro
{
    Todas,
    Livres,
    Ocupadas,
    PorReparar
}

/// <summary>Uma ferramenta numa posição do mapa, com o último estado registado.</summary>
public sealed record ArmazemMapaFerramenta(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    DateTime DesdeUtc,
    FerramentaEstadoTipo? Estado);

/// <summary>
/// Uma posição do mapa. <see cref="Par"/> diz que lá estão só a CM e o MF da
/// mesma referência e lote (guardam-se juntos); <see cref="Misturada"/> diz
/// que há ferramentas de referências ou lotes diferentes.
/// </summary>
public sealed record ArmazemMapaPosicao(int Id, string Codigo, bool Ativa, IReadOnlyList<ArmazemMapaFerramenta> Ferramentas)
{
    public bool Livre => Ferramentas.Count == 0;

    public bool Par => Ferramentas.Count == 2
        && Ferramentas[0].Tipo != Ferramentas[1].Tipo
        && Ferramentas.All(f => f.Tipo is ToolType.Cm or ToolType.Mf)
        && Ferramentas[0].Referencia == Ferramentas[1].Referencia
        && Ferramentas[0].Lote == Ferramentas[1].Lote;

    public bool Misturada => Ferramentas.Count > 1 && !Par;

    public bool PorReparar => Ferramentas.Any(f => f.Estado == FerramentaEstadoTipo.PorReparar);
}

/// <summary>
/// Uma estante: as posições do mesmo corredor e estante (os três primeiros
/// algarismos, 103… = corredor 1, estante 03).
/// </summary>
public sealed record ArmazemArmario(string Chave, int Posicoes, int Ocupadas)
{
    public string Corredor => ArmazemArmarios.Corredor(Chave);

    public int Percentagem => Posicoes == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / Posicoes, MidpointRounding.AwayFromZero);
}

/// <summary>
/// O mapa: as estantes com a ocupação e as posições do que se escolheu
/// (<see cref="Armario"/>: uma estante, um corredor ou «*», todas).
/// </summary>
public sealed record ArmazemMapaModelo(
    IReadOnlyList<ArmazemArmario> Armarios,
    string? Armario,
    int Posicoes,
    int Ocupadas,
    IReadOnlyList<ArmazemMapaPosicao> Lista)
{
    public int Percentagem => Posicoes == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / Posicoes, MidpointRounding.AwayFromZero);
}

/// <summary>O mapa do Armazém, como o ecrã do Access antigo: as posições por corredor e estante, livres e ocupadas.</summary>
public interface IArmazemMapa
{
    /// <summary>
    /// As estantes e as posições de <paramref name="armario"/> (uma estante,
    /// «103»; um corredor, «1»; null: a primeira estante; «*»: todas),
    /// filtradas. Só contam as posições ativas e as arquivadas com ferramentas.
    /// </summary>
    Task<ArmazemMapaModelo> MapaAsync(string? armario, ArmazemMapaFiltro filtro, CancellationToken cancellationToken = default);
}

/// <summary>
/// A regra da posição (Diogo 21/10, AR-03): quatro algarismos, o 1.º é o
/// corredor, o 2.º e o 3.º a estante e o 4.º a prateleira (1033 = corredor 1,
/// estante 03, prateleira 3). O mapa agrupa por corredor e por estante.
/// </summary>
public static class ArmazemArmarios
{
    public const string Todos = "*";

    /// <summary>A estante de uma posição: o corredor e a estante (1033 → 103).</summary>
    public static string De(string codigo) =>
        string.IsNullOrEmpty(codigo) ? string.Empty : codigo.Length < 3 ? codigo : codigo[..3];

    /// <summary>O corredor de uma estante ou de uma posição (103 → 1).</summary>
    public static string Corredor(string chave) =>
        string.IsNullOrEmpty(chave) ? string.Empty : chave[..1];

    /// <summary>A prateleira de uma posição (1033 → 3); null fora da regra.</summary>
    public static string? Prateleira(string codigo) =>
        codigo is { Length: 4 } ? codigo[3..] : null;

    /// <summary>O nome: «Corredor 1» para um corredor, «Corredor 1 · Estante 03» para uma estante.</summary>
    public static string Nome(string chave) => chave switch
    {
        { Length: 1 } => $"Corredor {chave}",
        { Length: 3 } => $"Corredor {chave[..1]} · Estante {chave[1..]}",
        _ => chave
    };
}

using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>O que o mapa mostra de cada armário.</summary>
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

/// <summary>Um armário: as posições que começam da mesma forma (40…, 70…, A…, E…).</summary>
public sealed record ArmazemArmario(string Chave, int Posicoes, int Ocupadas)
{
    public int Percentagem => Posicoes == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / Posicoes, MidpointRounding.AwayFromZero);
}

/// <summary>O mapa: os armários com a ocupação e as posições do armário escolhido.</summary>
public sealed record ArmazemMapaModelo(
    IReadOnlyList<ArmazemArmario> Armarios,
    string? Armario,
    int Posicoes,
    int Ocupadas,
    IReadOnlyList<ArmazemMapaPosicao> Lista)
{
    public int Percentagem => Posicoes == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / Posicoes, MidpointRounding.AwayFromZero);
}

/// <summary>O mapa do Armazém, como o ecrã do Access antigo: as posições por armário, livres e ocupadas.</summary>
public interface IArmazemMapa
{
    /// <summary>
    /// Os armários e as posições de <paramref name="armario"/> (null: o primeiro
    /// armário; «*»: todos), filtradas. Só contam as posições ativas e as
    /// arquivadas com ferramentas.
    /// </summary>
    Task<ArmazemMapaModelo> MapaAsync(string? armario, ArmazemMapaFiltro filtro, CancellationToken cancellationToken = default);
}

/// <summary>A regra do armário: as letras do início (A001 → A, E012 → E), senão os dois primeiros algarismos (4048 → 40, 7001A → 70).</summary>
public static class ArmazemArmarios
{
    public const string Todos = "*";

    public static string De(string codigo)
    {
        if (string.IsNullOrEmpty(codigo))
        {
            return string.Empty;
        }

        if (char.IsLetter(codigo[0]))
        {
            return new string(codigo.TakeWhile(char.IsLetter).ToArray());
        }

        var algarismos = new string(codigo.TakeWhile(char.IsDigit).ToArray());
        return algarismos.Length >= 3 ? algarismos[..2] : algarismos;
    }
}

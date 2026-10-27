namespace DMO.Alpha.Core.Tools;

/// <summary>
/// O HISTÓRICO DE UMA FERRAMENTA (Diogo, 22/10/2026): uma linha por Job On
/// (produção) em que a ferramenta trabalhou, com a produção, as datas, a %
/// de uso nessa produção e as OUTRAS ferramentas desse Job On (a própria
/// não aparece). A BQ liga-se pelo BqContext (bq_id): uma BQ trabalha com
/// MF diferentes. Sem ferramenta escolhida, a procura pela referência e
/// pelos filtros traz as mesmas linhas para as ferramentas encontradas.
/// </summary>
public sealed record HistoricoFerramenta(string ToolId, ToolType Tipo, string Referencia, string Lote);

public sealed record HistoricoLinha(
    int JobOnId,
    string Referencia,
    string Producao,
    string Maquina,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    decimal? Uso,
    HistoricoFerramenta Ferramenta,
    IReadOnlyList<HistoricoFerramenta> Cm,
    IReadOnlyList<HistoricoFerramenta> Mf,
    IReadOnlyList<HistoricoFerramenta> Bq,
    IReadOnlyDictionary<string, decimal?>? Usos = null)
{
    /// <summary>A % de uso de cada ferramenta deste Job On (a mesma conta para CM, MF e BQ); null sem leituras.</summary>
    public decimal? UsoDe(string toolId) => Usos is not null && Usos.TryGetValue(toolId, out var uso) ? uso : null;

    /// <summary>As ferramentas de um tipo neste Job On (a trocada também esteve nele).</summary>
    public IReadOnlyList<HistoricoFerramenta> DoTipo(ToolType tipo) => tipo switch
    {
        ToolType.Cm => Cm,
        ToolType.Mf => Mf,
        _ => Bq
    };

    /// <summary>
    /// Os dias da produção (Diogo, 22/10/2026): fim − início + 1, porque o dia
    /// do início conta (12/01 a 26/01 = 15). Ainda na máquina (sem fim), até
    /// hoje. Sem início, ou com o fim antes do início, null.
    /// </summary>
    public static int? Dias(DateOnly? inicio, DateOnly? fim, DateOnly hoje) =>
        inicio is { } de && (fim ?? hoje) is var ate && ate >= de ? ate.DayNumber - de.DayNumber + 1 : null;

    /// <summary>Os dias até hoje pela data de Portugal (o dia do negócio).</summary>
    public int? Dias() => Dias(InicioEm, FimEm, DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now));

    /// <summary>Os dois tipos que não são o da ferramenta, pela ordem CM, MF, BQ.</summary>
    public static IReadOnlyList<ToolType> OutrosTipos(ToolType tipo) =>
        new[] { ToolType.Cm, ToolType.Mf, ToolType.Bq }.Where(t => t != tipo).ToList();
}

/// <summary>O Lote filtra pelo lote da ferramenta (Diogo, 27/10/2026: «inserir lote»).</summary>
public sealed record HistoricoFiltro(string? Referencia, ToolType? Tipo, string? Linha, DateOnly? De, DateOnly? Ate, string? Lote = null)
{
    public bool ComFiltro => !string.IsNullOrWhiteSpace(Referencia) || Tipo is not null || !string.IsNullOrWhiteSpace(Linha) || De is not null || Ate is not null || !string.IsNullOrWhiteSpace(Lote);
}

public sealed record HistoricoProcura(IReadOnlyList<HistoricoLinha> Linhas, bool Cortada);

public interface IFerramentaHistorico
{
    /// <summary>A ferramenta (null quando não existe).</summary>
    Task<HistoricoFerramenta?> FerramentaAsync(string? toolId, CancellationToken cancellationToken = default);

    /// <summary>Os Job On da ferramenta, os mais recentes primeiro.</summary>
    Task<IReadOnlyList<HistoricoLinha>> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>A procura sem ferramenta escolhida (vazia sem filtro).</summary>
    Task<HistoricoProcura> ProcurarAsync(HistoricoFiltro filtro, CancellationToken cancellationToken = default);
}

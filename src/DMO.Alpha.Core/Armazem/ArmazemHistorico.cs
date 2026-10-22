using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// O filtro dos movimentos do Armazém (a lista «Últimas entradas e saídas»
/// da função Movimentos, que substituiu o Histórico; Diogo, 22/10/2026): o
/// texto (referência, lote ou posição), uma ferramenta, o tipo de movimento,
/// o destino da Saída, o intervalo de dias (datas locais, os dois
/// inclusive), o tipo de ferramenta, a referência, o lote exato, a Linha
/// (a ferramenta está associada a ela) e o reparador (a Saída para ele e a
/// Entrada que volta dele).
/// </summary>
public sealed record ArmazemHistoricoFiltro(
    string? Texto = null,
    string? ToolId = null,
    ArmazemMovimentoTipo? Tipo = null,
    ArmazemDestino? Destino = null,
    DateOnly? De = null,
    DateOnly? Ate = null,
    ToolType? TipoFerramenta = null,
    string? Referencia = null,
    string? Lote = null,
    string? Linha = null,
    string? Reparador = null);

/// <summary>Um movimento no Histórico, com a ferramenta (a referência e o lote guardados, se foi eliminada).</summary>
public sealed record ArmazemHistoricoLinha(
    int Id,
    DateTime OcorridoEmUtc,
    string? ToolId,
    ToolType? Tipo,
    string Referencia,
    string Lote,
    ArmazemMovimentoTipo Movimento,
    string Descricao,
    string? Observacoes,
    string? RegistadoPor)
{
    /// <summary>O destino da Saída.</summary>
    public ArmazemDestino? Destino { get; init; }

    /// <summary>O reparador: o da Saída para Reparação, ou, na Entrada que volta da Reparação, o da Saída antes dela.</summary>
    public string? Reparador { get; init; }

    /// <summary>A posição onde ficou (só na Entrada e no Corrigir para uma posição).</summary>
    public string? Posicao { get; init; }

    /// <summary>A ferramenta já foi eliminada: a linha fica, sem link.</summary>
    public bool Eliminada => ToolId is null;
}

/// <summary>Os movimentos do Armazém (antes o Histórico), do mais recente para o mais antigo.</summary>
public interface IArmazemHistorico
{
    Task<PagedList<ArmazemHistoricoLinha>> ListarAsync(ArmazemHistoricoFiltro filtro, int pagina, CancellationToken cancellationToken = default);

    /// <summary>Os reparadores que já aparecem nos movimentos (os nomes guardados), por ordem.</summary>
    Task<IReadOnlyList<string>> ReparadoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Os códigos das Linhas, por ordem.</summary>
    Task<IReadOnlyList<string>> LinhasAsync(CancellationToken cancellationToken = default);
}

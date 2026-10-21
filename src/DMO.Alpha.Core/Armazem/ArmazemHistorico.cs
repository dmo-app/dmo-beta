using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// O filtro do Histórico do Armazém: o texto (referência, lote ou posição),
/// uma ferramenta, o tipo de movimento, o destino da Saída e o intervalo de
/// dias (datas locais, os dois inclusive).
/// </summary>
public sealed record ArmazemHistoricoFiltro(
    string? Texto = null,
    string? ToolId = null,
    ArmazemMovimentoTipo? Tipo = null,
    ArmazemDestino? Destino = null,
    DateOnly? De = null,
    DateOnly? Ate = null);

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
    /// <summary>A ferramenta já foi eliminada: a linha fica, sem link.</summary>
    public bool Eliminada => ToolId is null;
}

/// <summary>O Histórico dos movimentos do Armazém, do mais recente para o mais antigo.</summary>
public interface IArmazemHistorico
{
    Task<PagedList<ArmazemHistoricoLinha>> ListarAsync(ArmazemHistoricoFiltro filtro, int pagina, CancellationToken cancellationToken = default);
}

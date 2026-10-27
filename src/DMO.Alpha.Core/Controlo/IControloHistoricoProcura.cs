using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// A procura do separador Histórico do Peso (?historico=1): os filtros
/// escritos, o dropdown das produções da referência e a página de
/// resultados. Saiu da página do Peso (Diogo, 27/10/2026: cada parte com
/// uma função clara, testável sozinha); a página só recebe o pedido, chama
/// esta procura e mostra o resultado.
/// </summary>
public interface IControloHistoricoProcura
{
    Task<ControloHistoricoResultado> ProcurarAsync(ControloHistoricoPedido pedido, CancellationToken cancellationToken = default);
}

/// <summary>
/// Os filtros como vêm no pedido (já sem espaços à volta; vazio = null).
/// <see cref="Producao"/> é o valor do dropdown, «nº|referência», ou só o
/// nº nos links antigos; <see cref="Tipo"/> e <see cref="Data"/> são o
/// texto do select e da data.
/// </summary>
public sealed record ControloHistoricoPedido(
    string? Referencia,
    string? Producao,
    string? Ferramenta,
    string? Lote,
    string? Tipo,
    string? Data,
    bool SemJobOn,
    int? Pagina);

/// <summary>
/// O que o separador mostra: os filtros lidos, as produções do dropdown, a
/// produção escolhida (o nº e a referência dela) e, só com pelo menos um
/// filtro, a página de resultados.
/// </summary>
public sealed record ControloHistoricoResultado(
    string? Referencia,
    string? Producao,
    string? ProducaoReferencia,
    string? Ferramenta,
    string? Lote,
    ControloHistoryKind? Tipo,
    DateOnly? Data,
    bool SemJobOn,
    IReadOnlyList<JobOnProductionOption> Producoes,
    PagedList<ControloHistorySearchItem>? Pagina)
{
    /// <summary>O separador abre vazio: só procura com pelo menos um filtro.</summary>
    public bool ComProcura =>
        SemJobOn || Referencia is not null || Producao is not null || Ferramenta is not null
        || Lote is not null || Tipo is not null || Data is not null;
}

/// <summary>O separador entre o nº e a referência no valor do dropdown das produções.</summary>
public static class ControloHistoricoValores
{
    public const char ProducaoSeparador = '|';
}

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O tipo de registo do histórico do Controlo (o html: Peso, Pegamentos e
/// Comparação na mesma lista; o Resumo de cada produção junta-se a eles).
/// </summary>
public enum ControloHistoryKind
{
    Peso = 0,
    Pegamentos = 1,
    Comparacao = 2,

    /// <summary>O Resumo de uma produção (um por controlo_id; o Id da linha é o jobon_id); a data é a da aprovação.</summary>
    Resumo = 3
}

/// <summary>
/// Uma linha do histórico do Controlo (a secção history do dmo-html): um
/// registo guardado de qualquer dos tipos, identificado pelos FATOS
/// HUMANOS (a referência/produção, a ferramenta, a data quando existe). O
/// par (Kind, Id) é a âncora do POST/URL — nunca um facto da folha. DataUtc
/// é a data gravada do registo: nos Pesos e nos Pegamentos o carimbo do
/// registo (null nos anteriores a 17/10/2026, sem data inventada), nas
/// comparações a gravação, no Resumo a aprovação (null por aprovar).
/// Nos Pesos e Pegamentos sem Job On, ControloId é null, a referência é a da
/// CM e o nº de produção é «Sem Job On».
/// </summary>
public sealed record ControloHistorySearchItem(
    ControloHistoryKind Kind,
    int Id,
    int? ControloId,
    string Reference,
    string ProductionNumber,
    string Machine,
    string? ToolLabel,
    DateTime? DataUtc,
    string? Resumo)
{
    /// <summary>O tool_id da CM do registo (null quando não há ou foi eliminada).</summary>
    public string? CmToolId { get; init; }

    /// <summary>A posição do armazém onde a CM está agora (null se não está no armazém).</summary>
    public string? CmPosicao { get; init; }
}

/// <summary>
/// A procura do HISTÓRICO DO CONTROLO ao uso do dmo-html: os registos
/// guardados (Peso, Pegamentos, Comparação e o Resumo de cada produção) com
/// os filtros da oficina — a referência do conjunto (pelo início dela ou da
/// parte BQ, nunca a meio), o nº de produção (igual, com a referência da
/// produção escolhida), a ferramenta (a referência da CM congelada ou
/// corrente, pela mesma regra), o tipo e a data (o dia de Lisboa, pela data
/// gravada de cada registo). Do mais recente para o mais antigo; os
/// registos sem data no fim. Com <c>semJobOn</c>, só os Pesos e os
/// Pegamentos ainda sem Job On associado: não têm produção, a referência
/// procura-se nas ferramentas do registo e a data é a do registo.
/// </summary>
public interface IControloHistorySearchQuery
{

    /// <summary>
    /// A mesma procura, uma página de cada vez (paginada na query).
    /// <paramref name="productionReference"/> é a referência da produção
    /// escolhida no dropdown: o nº só é único dentro da referência.
    /// </summary>
    Task<DMO.Alpha.Core.Paging.PagedList<ControloHistorySearchItem>> SearchPageAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        int? pageNumber,
        int pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default,
        bool semJobOn = false,
        string? productionReference = null);

    /// <summary>
    /// A produção pedida (por exemplo por um link) que já não está entre as
    /// últimas do dropdown: a mais recente com este nº entre as referências
    /// que correspondem ao texto, pela regra da tabela, e com
    /// <paramref name="productionReference"/> só a dessa referência. Null
    /// quando nenhuma dessas referências tem o nº.
    /// </summary>
    Task<DMO.Alpha.Core.Production.JobOnProductionOption?> ProducaoDaReferenciaAsync(
        string reference,
        string productionNumber,
        string? productionReference,
        CancellationToken cancellationToken = default);
}

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
/// registo guardado de qualquer dos três tipos, identificado pelos FATOS
/// HUMANOS (a referência/produção, a ferramenta, a data quando existe). O
/// par (Kind, Id) é a âncora do POST/URL — nunca um facto da folha. DataUtc
/// só existe nos tipos que registam data (as comparações); os Pesos e os
/// Pegamentos guardados não têm data persistida — a procura por data devolve
/// as comparações (o aviso vive na apresentação, nunca um facto inventado).
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
    string? Resumo);

/// <summary>
/// A procura do HISTÓRICO DO CONTROLO ao uso do dmo-html: os registos
/// guardados dos três tipos (Peso, Pegamentos, Comparação) com os filtros da
/// oficina — a referência do conjunto, o nº de produção, a ferramenta (a
/// referência da CM congelada ou corrente), o tipo e a data. Do mais
/// recente para o mais antigo. Com <c>semJobOn</c>, só os Pesos e os
/// Pegamentos ainda sem Job On associado (filtrados pela ferramenta; a
/// referência, a produção e a data não se aplicam, porque não as têm).
/// </summary>
public interface IControloHistorySearchQuery
{
    Task<IReadOnlyList<ControloHistorySearchItem>> SearchAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        CancellationToken cancellationToken = default,
        bool semJobOn = false);

    /// <summary>A mesma procura, uma página de cada vez (paginada na query).</summary>
    Task<DMO.Alpha.Core.Paging.PagedList<ControloHistorySearchItem>> SearchPageAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        int? pageNumber,
        int pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default,
        bool semJobOn = false);
}

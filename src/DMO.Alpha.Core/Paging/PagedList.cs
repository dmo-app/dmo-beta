namespace DMO.Alpha.Core.Paging;

/// <summary>
/// As regras da paginação das listas em tabela (o Owner 2026-10-16: «tudo
/// que sejam listas em tabelas limitar numero e criar paginaçao»).
/// </summary>
public static class ListPaging
{
    /// <summary>As linhas por página das listas em tabela.</summary>
    public const int DefaultPageSize = 25;

    public static int TotalPages(int totalCount, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

    /// <summary>A página pedida ajustada a [1, última página].</summary>
    public static int ClampPage(int? pageNumber, int totalCount, int pageSize) =>
        Math.Clamp(pageNumber ?? 1, 1, TotalPages(totalCount, pageSize));
}

/// <summary>
/// Uma página de uma lista em tabela: os itens da página, o total do filtro
/// e a página efetiva (já ajustada ao intervalo válido).
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => ListPaging.TotalPages(TotalCount, PageSize);

    public static PagedList<T> Empty { get; } = new([], 0, 1, ListPaging.DefaultPageSize);

    /// <summary>
    /// Pagina uma lista já carregada — só para os casos em que o conjunto
    /// completo é preciso de qualquer forma (totais, cálculos, PDF).
    /// </summary>
    public static PagedList<T> FromLoaded(IReadOnlyList<T> all, int? pageNumber, int pageSize = ListPaging.DefaultPageSize)
    {
        var page = ListPaging.ClampPage(pageNumber, all.Count, pageSize);
        return new(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count, page, pageSize);
    }
}

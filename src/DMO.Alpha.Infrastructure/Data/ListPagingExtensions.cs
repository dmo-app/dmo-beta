using DMO.Alpha.Core.Paging;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Data;

public static class ListPagingExtensions
{
    /// <summary>
    /// A paginação feita NA QUERY: o COUNT do filtro e o Skip/Take da página
    /// (a query tem de chegar já ordenada). A página pedida é ajustada à
    /// última existente.
    /// </summary>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        int? pageNumber,
        int pageSize = ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var total = await query.CountAsync(cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedList<T>(items, total, page, pageSize);
    }
}

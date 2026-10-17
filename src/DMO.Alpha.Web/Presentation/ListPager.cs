using DMO.Alpha.Core.Paging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// A barra de paginação de uma lista em tabela (o partial _ListPager). Os
/// links repetem o pedido atual (filtros, ordenação, separador) e só trocam
/// o parâmetro da página — cada tabela da mesma folha tem o seu.
/// </summary>
public sealed record ListPager(int PageNumber, int TotalPages, int TotalCount, string Param = "pagina", string? Fragment = null)
{
    public static ListPager For<T>(PagedList<T> page, string param = "pagina", string? fragment = null) =>
        new(page.PageNumber, page.TotalPages, page.TotalCount, param, fragment);

    public string Href(HttpRequest request, int pageNumber)
    {
        var query = request.Query
            .Where(pair => !string.Equals(pair.Key, Param, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pair.Key, "handler", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, StringValues>(pair.Key, value)))
            .ToList();
        if (pageNumber > 1)
        {
            query.Add(new(Param, pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return QueryHelpers.AddQueryString(request.PathBase + request.Path, query) + (Fragment is null ? "" : "#" + Fragment);
    }
}

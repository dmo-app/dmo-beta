namespace DMO.Alpha.Core.Search;

/// <summary>
/// As fontes fechadas das sugestões ao escrever (o Owner 2026-10-16: «ao
/// procurar deveria mostrar com base no que escrevo e começar a filtrar,
/// alias isso deveria afetar todos campos»). Cada fonte é um campo de
/// procura de registos; nunca texto livre vindo do browser.
/// </summary>
public enum SuggestionSource
{
    /// <summary>Produções (Job On): referência ou nº de produção; o valor
    /// é a referência quando é ela que corresponde, senão o nº.</summary>
    Producoes = 1,

    /// <summary>Ferramentas: referência (opcionalmente de um só tipo).</summary>
    Ferramentas = 2,

    /// <summary>Reparadores das boquilhas.</summary>
    Reparadores = 3,

    /// <summary>Referências de produção distintas (o valor é a referência).</summary>
    Referencias = 4,

    /// <summary>Números de produção (o valor é o nº).</summary>
    NumerosProducao = 5
}

/// <summary>Uma sugestão: o valor que entra no campo e o texto mostrado.</summary>
public sealed record Suggestion(string Value, string Label);

/// <summary>
/// Sugestões leves para os campos de procura: a partir de
/// <see cref="MinimumLength"/> caracteres, sem distinção de maiúsculas, por
/// parte do texto, no máximo <see cref="MaxResults"/>. Uma só consulta por
/// pedido, sem carregar registos completos.
/// </summary>
public interface ISuggestionQuery
{
    public const int MinimumLength = 2;
    public const int MaxResults = 10;

    Task<IReadOnlyList<Suggestion>> SuggestAsync(
        SuggestionSource source,
        string? term,
        DMO.Alpha.Core.Tools.ToolType? toolType = null,
        CancellationToken cancellationToken = default);
}

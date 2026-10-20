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
    NumerosProducao = 5,

    /// <summary>
    /// Referências de produção e referências de BQ juntas (o valor é a
    /// referência; as de BQ vêm marcadas «BQ»): a pesquisa do Histórico das
    /// Boquilhas, que procura nas duas.
    /// </summary>
    ReferenciasEBq = 6,

    /// <summary>
    /// O «Registar ferramenta real» (Diogo, 19/10/2026): as ferramentas do
    /// tipo (na CM também pela referência MF associada, com o nome original)
    /// e as referências completas das produções. O valor é sempre a
    /// referência toda.
    /// </summary>
    ReferenciasRegisto = 7
}

/// <summary>Uma sugestão: o valor que entra no campo e o texto mostrado.</summary>
public sealed record Suggestion(string Value, string Label);

/// <summary>
/// Sugestões leves para os campos de procura, sem distinção de maiúsculas, no
/// máximo <see cref="MaxResults"/>. As referências encontram-se pelo início
/// delas ou da parte BQ, nunca a meio (Diogo, 18/10/2026), e por isso bastam
/// <see cref="MinimumLength"/> caracteres («0» sugere a 0132T173); os nºs de
/// produção pelo início. Os nomes dos reparadores encontram-se por parte do
/// nome, a partir de <see cref="NameMinimumLength"/> caracteres. Uma só
/// consulta por pedido, sem carregar registos completos.
/// </summary>
public interface ISuggestionQuery
{
    /// <summary>O mínimo de caracteres das referências, das produções e das ferramentas.</summary>
    public const int MinimumLength = 1;

    /// <summary>O mínimo de caracteres dos nomes (os reparadores), que se procuram a meio.</summary>
    public const int NameMinimumLength = 2;

    public const int MaxResults = 10;

    Task<IReadOnlyList<Suggestion>> SuggestAsync(
        SuggestionSource source,
        string? term,
        DMO.Alpha.Core.Tools.ToolType? toolType = null,
        CancellationToken cancellationToken = default);
}

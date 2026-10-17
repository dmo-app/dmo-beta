namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface só de leitura de descoberta de candidatos de Tool para seleção
/// humana explícita (futuro picker Ferramentas/Boquilhas). Não é um
/// repositório genérico de Tools nem uma API de pesquisa de domínio
/// completo: o contrato é apenas a lista de candidatos que satisfazem os
/// filtros explícitos do <see cref="ToolCandidateFilter"/>.
/// Os filtros apenas reduzem o conjunto de candidatos; nunca selecionam,
/// nunca inferem tool_id de referência/lote/tipo e nunca auto-selecionam —
/// mesmo quando resta exatamente um candidato, esse candidato permanece
/// por selecionar por um ato humano explícito.
/// </summary>
public interface IToolCandidateQuery
{
    /// <summary>
    /// Devolve os candidatos de Tool que satisfazem os filtros explícitos,
    /// projetados dos factos atualmente persistidos no registo canónico e
    /// ordenados por tool_id (identidade canónica) para comportamento
    /// determinístico. Um filtro null significa que nenhuma dimensão é
    /// aplicada (o registo completo de candidatos); valores em branco numa
    /// dimensão tratam essa dimensão como não aplicada. O resultado é
    /// sempre uma lista de candidatos — nunca um candidato "resolvido",
    /// mesmo quando a filtragem deixa exatamente um candidato.
    /// </summary>
    Task<IReadOnlyList<ToolCandidate>> FindCandidatesAsync(
        ToolCandidateFilter? filter,
        CancellationToken cancellationToken = default);
    /// <summary>Página de 10 ou 20 resultados, com os mesmos filtros e ordenação da descoberta.</summary>
    Task<ToolCandidatePage> FindPageAsync(ToolCandidateFilter? filter, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);


}

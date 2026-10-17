namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Surface só de leitura do Peso: resolução de um Peso pelo seu peso_id
/// canónico (com as leituras persistidas e os valores derivados do cálculo
/// principal) e a lista do histórico de Pesos de uma Tool, para que um Peso
/// tomado antes da produção permaneça diretamente consultável a partir da
/// sua ferramenta. Não é um repositório genérico: as projeções são
/// purpose-specific do surface Peso e o cálculo é o detido pelo backend.
/// </summary>
public interface IPesoQuery
{
    /// <summary>
    /// Devolve o read model do Peso pedido, ou null quando o peso_id não
    /// existe. Os valores derivados são calculados a partir dos inputs
    /// consumidos persistidos no Peso e das leituras persistidas — nunca a
    /// partir do estado corrente das Tools ou da configuração.
    /// </summary>
    Task<PesoReadModel?> FindPesoAsync(int pesoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve o histórico de Pesos da Tool (pesos cuja CM pesada é o
    /// tool_id pedido), do mais recente para o mais antigo, com os valores
    /// de resumo calculados a partir dos dados persistidos de cada Peso.
    /// </summary>
    Task<IReadOnlyList<PesoToolHistoryItem>> ListByToolAsync(
        string toolId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O Peso baseline associado ao controlo de uma produção (o Peso
    /// production-scoped do corte C) — ou null quando o controlo ainda não
    /// tem o seu Peso (get-or-open: a associação é 1:1 e a criação é do
    /// fluxo com a temperatura medida).
    /// </summary>
    Task<PesoReadModel?> FindByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);
}

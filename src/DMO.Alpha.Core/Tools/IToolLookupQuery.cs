namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface nativo, canónico e só de leitura para resolver uma Tool pelo seu
/// tool_id canónico exato no fluxo do Registo. Não é um repositório genérico:
/// nunca deriva identidade de referência/lote/tipo, nunca cria Tools nem muta
/// estado, e devolve null quando o tool_id é desconhecido.
/// </summary>
public interface IToolLookupQuery
{
    /// <summary>
    /// Devolve o read model da Tool cujo tool_id canónico coincide exatamente
    /// com o pedido, ou null quando esse tool_id não existe no registo. Um
    /// toolId nulo, vazio ou constituído apenas por espaços é tratado como um
    /// tool_id desconhecido e devolve null (sem lançar exceção de argumento).
    /// </summary>
    Task<ToolLookupResult?> FindByToolIdAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As Tools dos tool_id pedidos numa só consulta, indexadas pelo tool_id,
    /// com a mesma regra de <see cref="FindByToolIdAsync"/>. Os ids nulos,
    /// vazios ou desconhecidos ficam de fora.
    /// </summary>
    Task<IReadOnlyDictionary<string, ToolLookupResult>> FindByToolIdsAsync(IEnumerable<string?> toolIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// A Tool do tipo pedido com essa referência (sem distinguir maiúsculas) e
    /// o lote mais alto, ou null. O lote é comparado pelo comprimento e depois
    /// pelo texto, para que «10» venha depois de «9».
    /// </summary>
    Task<ToolLookupResult?> FindByReferenceAsync(string reference, ToolType type, CancellationToken cancellationToken = default);
}

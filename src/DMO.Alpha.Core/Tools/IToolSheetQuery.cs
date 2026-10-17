namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface só de leitura da ficha de uma Tool: resolução cirúrgica por
/// tool_id canónico exato, composição purpose-specific no momento da
/// leitura (factos + máquinas + contexto técnico). Não é um repositório
/// genérico: devolve null quando o tool_id não existe ou é a linha DUMMY
/// (REMOVED) — nenhuma ficha é inventada.
/// </summary>
public interface IToolSheetQuery
{
    /// <summary>
    /// Devolve a ficha da Tool cujo tool_id coincide exatamente com o
    /// pedido, ou null quando o tool_id não existe no registo ou é a linha
    /// DUMMY canónica (excluída por constante em TODAS as queries). Um
    /// toolId nulo, vazio ou em branco é tratado como desconhecido.
    /// </summary>
    Task<ToolSheetReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default);
}

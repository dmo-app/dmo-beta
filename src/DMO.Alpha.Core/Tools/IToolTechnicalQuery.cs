namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface só de leitura do contexto técnico estável de uma Tool (regra
/// Ferramentas «Tool-owned technical values»), resolvido por tool_id canónico
/// exato. Não é um repositório genérico: devolve null quando o tool_id não
/// existe ou quando a Tool ainda não tem linha de contexto técnico (os
/// valores técnicos não têm de existir quando o tool_id é criado). A leitura
/// inclui TODAS as colunas técnicas persistidas; cada consumidor usa as que
/// lhe dizem respeito.
/// </summary>
public interface IToolTechnicalQuery
{
    /// <summary>
    /// Devolve o contexto técnico da Tool cujo tool_id coincide exatamente
    /// com o pedido, ou null quando o tool_id não existe no registo ou a Tool
    /// ainda não tem valores técnicos registados. Um toolId nulo, vazio ou
    /// em branco é tratado como tool_id desconhecido e devolve null.
    /// </summary>
    Task<ToolTechnicalReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default);
}

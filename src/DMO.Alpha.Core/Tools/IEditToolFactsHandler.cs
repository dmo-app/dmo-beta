namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface de escrita canónica: substituir integralmente os factos de uma
/// Tool existente (o mesmo tool_id). Recusas tipadas do enum fechado
/// verificadas ANTES de qualquer escrita; a substituição de máquinas é
/// atómica com a dos factos (uma única escrita).
/// </summary>
public interface IEditToolFactsHandler
{
    Task<EditToolFactsOutcome> HandleAsync(
        EditToolFacts command,
        CancellationToken cancellationToken = default);
}

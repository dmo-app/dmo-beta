namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface de escrita canónica: duplicar uma Tool existente em criação
/// assistida (novo tool_id mintado, lote novo obrigatório, fonte intacta).
/// Recusas tipadas ANTES de qualquer escrita.
/// </summary>
public interface IDuplicateToolHandler
{
    Task<DuplicateToolOutcome> HandleAsync(
        DuplicateTool command,
        CancellationToken cancellationToken = default);
}

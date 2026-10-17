namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface de escrita canónica: substituir integralmente os valores
/// técnicos Tool-owned de uma Tool existente (cria a linha de contexto
/// técnico quando ainda não existe). Recusas tipadas ANTES de qualquer
/// escrita; gravação sempre no tool_id owner.
/// </summary>
public interface IEditToolTechnicalValuesHandler
{
    Task<EditToolTechnicalValuesOutcome> HandleAsync(
        EditToolTechnicalValues command,
        CancellationToken cancellationToken = default);
}

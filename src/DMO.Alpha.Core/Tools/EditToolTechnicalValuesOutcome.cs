namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado da edição dos valores técnicos: o outcome tipado e, em caso
/// de sucesso, o read model técnico da Tool já substituído.
/// </summary>
public sealed record EditToolTechnicalValuesOutcome(
    EditToolTechnicalValuesResult Result,
    ToolTechnicalReadModel? Technical = null);

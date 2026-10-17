namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da edição dos valores técnicos de uma Tool. Recusa
/// partilhada: ToolNotFound; o token de processo (quando fornecido) é
/// validado tipadamente (NNPB/PS) como no Peso.
/// </summary>
public enum EditToolTechnicalValuesResult
{
    Success,

    /// <summary>
    /// A tool_id não resolve a Tool canónica no registo (inclui a linha
    /// DUMMY REMOVED, que nunca é endereçável).
    /// </summary>
    ToolNotFound,

    /// <summary>O token do processo fornecido não é um dos autorizados (NNPB, PS).</summary>
    UnknownProcessToken,

    /// <summary>A supplied numeric field is malformed; an empty field remains valid.</summary>
    InvalidNumericValue
}

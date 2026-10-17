namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado da edição dos factos: o outcome tipado e, em caso de sucesso,
/// o read model da Tool com os factos já substituídos e a nota avisadora
/// de formato (o formato NUNCA recusa — registo truthful).
/// </summary>
public sealed record EditToolFactsOutcome(
    EditToolFactsResult Result,
    EditedToolFacts? Tool = null,
    // Na recusa LotAlreadyExists: o tool_id da ferramenta que já tem o
    // mesmo tipo, referência e lote (a ficha oferece abri-la).
    string? ExistingToolId = null);

/// <summary>
/// Read model purpose-specific da Tool após a edição dos factos: o mesmo
/// tool_id (a identidade nunca muda) com os factos substituídos.
/// </summary>
public sealed record EditedToolFacts(
    string ToolId,
    ToolType Type,
    string Reference,
    string Lot,
    string? AssociatedMfReference,
    int? BqAccountedQuantity,
    bool ReferenceFormatAdvisory,
    // TRUE quando o lote chegou vazio e ficou «s/lote» (beta.63).
    bool LotDefaulted = false);

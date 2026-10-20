namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado da duplicação: o outcome tipado e, em caso de sucesso, o
/// read model da NOVA Tool (tool_id mintado + factos copiados como
/// defaults editáveis, com o lote novo).
/// </summary>
public sealed record DuplicateToolOutcome(
    DuplicateToolResult Result,
    CreatedTool? Tool = null,
    // Na recusa LotAlreadyExists: a ferramenta que já tem esse lote.
    string? ExistingToolId = null);

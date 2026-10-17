namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Read model dos factos de Tool atualmente suportados para o surface de
/// leitura nativo do Registo: o tool_id é a ÚNICA identidade canónica,
/// enquanto type/reference/lot são apenas factos descritivos/de exibição e
/// NUNCA derivam identidade nem servem de gate comportamental. Não é um
/// repositório genérico de Tools.
/// </summary>
public sealed record ToolLookupResult(
    string ToolId,
    ToolType Type,
    string Reference,
    string Lot);

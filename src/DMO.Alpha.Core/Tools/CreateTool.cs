namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Compatibilidades Tool–máquina: CM, MF e BQ admitem várias máquinas.
/// Compatibilidade não determina a máquina em que a ferramenta está em uso.
/// A identidade continua a ser o ToolId; lotes diferentes mantêm identidades distintas.
/// </summary>
public sealed record CreateTool(
    string TypeToken,
    string Reference,
    string Lot,
    string? AssociatedMfReference = null,
    int? BqAccountedQuantity = null,
    IReadOnlyList<int>? MachineIds = null,
    // O NOME TÉCNICO (o código do desenho — o que está no SAP; o Owner:
    // «se introduzirmos o nome tecnico, ou seja o que esta no sap.
    // apresenta logo tudo»): guardado como o operador escreveu; o parser
    // canónico decodifica os factos na ficha. Null = o formato simples.
    string? TechnicalName = null,
    bool UseLotQuantityDefault = true,
    // Quem regista (fica no aviso da ferramenta existente se for repetida).
    string? RequestedBy = null);

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Compatibilidades Tool–máquina: CM, MF e BQ admitem várias máquinas.
/// Compatibilidade não determina a máquina em que a ferramenta está em uso.
/// A identidade continua a ser o ToolId; lotes diferentes mantêm identidades distintas.
/// </summary>
public sealed record EditToolFacts(
    string ToolId,
    string TypeToken,
    string Reference,
    string Lot,
    string? AssociatedMfReference = null,
    int? BqAccountedQuantity = null,
    IReadOnlyList<int>? MachineIds = null);

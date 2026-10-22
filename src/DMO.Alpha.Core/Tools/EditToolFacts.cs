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
    IReadOnlyList<int>? MachineIds = null,
    // A versão da linha quando a ficha foi aberta (a página envia-a escondida);
    // null = sem verificação. Diferente da gravada = ChangedByAnotherUser.
    uint? ExpectedVersion = null,
    // Quem edita (fica no aviso da outra ferramenta se ficar repetida).
    string? RequestedBy = null,
    // O nome técnico (SAP), no mesmo formulário do registo (Diogo, 20/10/2026:
    // um só formulário de ferramenta). null = fica como está; vazio = limpa.
    string? TechnicalName = null,
    // A fábrica de origem (ex.: MG). null = fica como está; vazio = limpa.
    string? FabricaOrigem = null);

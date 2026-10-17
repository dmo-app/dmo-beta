using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Read model purpose-specific da FICHA de uma Tool (Ferramentas): todos os
/// factos humanos do registo — tipo, referência, lote, referência MF
/// associada (descoberta), quantidade contabilística BQ, as máquinas
/// compatíveis (ligações Tool–máquina) e o contexto técnico Tool-owned
/// quando existe (a ausência é estado válido — os valores técnicos não
/// têm de existir quando o tool_id é criado). Projetado dos factos
/// persistidos, exatamente como estão; o tool_id é a única identidade
/// canónica. A linha DUMMY nunca resolve (constante REMOVED).
/// </summary>
public sealed record ToolSheetReadModel(
    string ToolId,
    ToolType Type,
    string Reference,
    string Lot,
    string? AssociatedMfReference,
    int? BqAccountedQuantity,
    IReadOnlyList<MachineOption> Machines,
    ToolTechnicalReadModel? Technical,
    /// <summary>O nome técnico (o código do desenho — o que está no SAP); null = o formato simples.</summary>
    string? TechnicalName = null);

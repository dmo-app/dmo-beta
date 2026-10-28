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
    string? TechnicalName = null,
    /// <summary>A versão da Tool gravada (vai escondida no Editar; ver EditToolFacts.ExpectedVersion).</summary>
    uint Version = 0,
    /// <summary>A versão da informação técnica gravada; 0 = ainda sem linha técnica.</summary>
    uint TechnicalVersion = 0,
    /// <summary>O aviso de ferramenta repetida (Tool.DuplicateWarningAtUtc); null = sem aviso.</summary>
    DateTime? DuplicateWarningAtUtc = null,
    string? DuplicateWarningBy = null,
    /// <summary>A fábrica de origem (ex.: MG); null = não indicada.</summary>
    string? FabricaOrigem = null,
    /// <summary>A quantidade de AN do lote (Tool.AnQuantity); null = vazia.</summary>
    int? AnQuantity = null,
    /// <summary>A B2 em que a ferramenta trabalha (Dupla/Tripla); null = sem escolha.</summary>
    B2Modo? B2Modo = null);

using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Os factos das ferramentas herdadas pela folha do Peso.
/// Tool null para tool_id desconhecido ou REMOVED; técnico null sem linha
/// técnica; densidade do vidro só quando a CM tem processo configurado.
/// </summary>
public sealed record PesoToolFacts(
    ToolLookupResult? CmTool,
    ToolTechnicalReadModel? CmTechnical,
    decimal? CmGlassDensity,
    ToolLookupResult? BqTool,
    ToolTechnicalReadModel? BqTechnical,
    ToolLookupResult? MfTool);

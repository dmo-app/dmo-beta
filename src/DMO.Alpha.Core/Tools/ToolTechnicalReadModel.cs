namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Read model purpose-specific do contexto técnico Tool-owned: os valores
/// técnicos estáveis atualmente persistidos para o tool_id (processo CM, peso
/// nominal CM, volume_puncao CM, volume_marisa BQ, diametro_gargalo e a
/// geometria TP de referência — D, R_draw, sagitta/h_ref, R base, R esfera e
/// volume, registados tal como vêm da folha: a app NUNCA recalcula os
/// derivados), projetados exatamente como estão persistidos. O tool_id é a
/// única identidade canónica; os valores são factos técnicos da Tool, nunca
/// identidade nem gate comportamental. Aditivo por posição: consumidores
/// existentes (a página Peso) só leem os campos que lhes dizem respeito —
/// zero quebras.
/// </summary>
public sealed record ToolTechnicalReadModel(
    string ToolId,
    CmProcess? Process,
    decimal? NominalWeightGrams,
    decimal? VolumePuncaoCm3,
    decimal? VolumeMarisaCm3,
    decimal? DiametroGargaloMm,
    decimal? TpDiametroMm = null,
    decimal? TpRaioMm = null,
    decimal? TpSagittaMm = null,
    decimal? TpRaioBaseMm = null,
    decimal? TpRaioEsferaMm = null,
    decimal? TpVolumeMm3 = null);

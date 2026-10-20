namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Comando: substituir integralmente os VALORES TÉCNICOS da Tool (todas
/// as colunas do ToolTechnicalContext: processo CM, pesos/volumes/gargalo
/// e a geometria TP de referência). O valor vazio no formulário é NULL —
/// o estado «em falta» é válido e nunca é inventado; a linha de contexto
/// técnico é CRIADA quando ainda não existe. Os valores gravam sempre no
/// tool_id owner (regra Ferramentas «Tool-owned technical values»): são
/// factos estáveis do tool_id, nunca uma cópia privada de um consumidor.
/// A app NUNCA recalcula os derivados TP (R base, R esfera, volume): o
/// registo é truthful aos valores fornecidos.
/// </summary>
public sealed record EditToolTechnicalValues(
    string ToolId,
    string? ProcessToken = null,
    decimal? NominalWeightGrams = null,
    decimal? VolumePuncaoCm3 = null,
    decimal? VolumeMarisaCm3 = null,
    decimal? DiametroGargaloMm = null,
    decimal? TpDiametroMm = null,
    decimal? TpRaioMm = null,
    decimal? TpSagittaMm = null,
    decimal? TpRaioBaseMm = null,
    decimal? TpRaioEsferaMm = null,
    decimal? TpVolumeMm3 = null,
    // A versão da linha quando a ficha foi aberta (0 = ainda sem linha
    // técnica); null = sem verificação. Diferente da gravada = ChangedByAnotherUser.
    uint? ExpectedVersion = null);

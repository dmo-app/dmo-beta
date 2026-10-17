namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Comando: eliminar uma Tool do registo canónico — ação corretiva
/// EXPLÍCITA do Owner (não o ciclo de vida normal). O handler executa a
/// transação canónica: backfill dos factos congelados em falta →
/// reatribuição dos registos de consumo (Pesos, Pegamentos) à linha DUMMY
/// canónica → limpeza da âncora direta dos traces não resolvidos (ToolId
/// = NULL) → eliminação das ligações Tool–máquina e do contexto técnico →
/// apagate FÍSICA da Tool. Os registos de consumo CONGELAM
/// referência+lote da tool (à criação ou no backfill): o histórico humano
/// sobrevive à tool. Os contextos de produção (strings penduradas,
/// verdade histórica) não são tocados.
/// </summary>
public sealed record DeleteTool(string ToolId);

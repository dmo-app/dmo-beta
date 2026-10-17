namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Comando: duplicar uma Tool — criação assistida de OUTRA Tool real, não
/// mutação da identidade da fonte. Copia os factos (tipo, referência,
/// referência MF associada, quantidade contabilística BQ, máquinas) e os
/// valores técnicos (incl. geometria TP) como DEFAULTS EDITÁVEIS (herança
/// de conveniência, nunca imutável); o LOTE NOVO é OBRIGATÓRIO (recusa
/// MissingNewLot — um lote diferente é uma Tool diferente e o novo tool_id
/// é mintado pelo backend). A fonte fica intacta.
/// </summary>
public sealed record DuplicateTool(
    string SourceToolId,
    string NewLot);

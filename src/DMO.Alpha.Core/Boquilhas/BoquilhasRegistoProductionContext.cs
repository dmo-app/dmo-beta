namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Read model purpose-specific do Registo: o contexto de produção Job On
/// alcançável através do bq_id associado ao trace. Contexto apenas — o
/// Registo lê, nunca escreve nem decide nada sobre o Job On. As datas de
/// início e fim da produção aparecem nos cartões do cabeçalho do Registo.
/// </summary>
public sealed record BoquilhasRegistoProductionContext(
    string Reference,
    string ProductionNumber,
    string MachineCode,
    DateOnly? InicioEm = null,
    DateOnly? FimEm = null);

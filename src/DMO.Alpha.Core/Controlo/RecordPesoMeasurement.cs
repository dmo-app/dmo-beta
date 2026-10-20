namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: registar uma leitura física real (measurement row) num peso_id
/// existente. Contém apenas os factos da medição: o número/posição CM visível
/// registado pelo operador (row-level measurement data, nunca um cm_id/tool_id)
/// e a massa de água observada na totalidade. A posição física da linha é
/// atribuída pelo backend pela ordem de registo; os valores derivados
/// (capacidade, peso do vidro) são calculados pelo backend a partir dos
/// inputs consumidos do Peso e nunca são fornecidos pelo cliente.
/// <see cref="AdditionalRows"/>: as outras linhas do mesmo envio (o «+»),
/// gravadas a seguir à primeira na mesma escrita — ficam todas ou nenhuma.
/// </summary>
public sealed record RecordPesoMeasurement(
    int PesoId,
    string? CmSubject,
    decimal WaterWeightGrams,
    IReadOnlyList<PesoMeasurementRow>? AdditionalRows = null);

/// <summary>Uma linha a mais do mesmo envio: o N.º CM visível e a massa de água.</summary>
public sealed record PesoMeasurementRow(string? CmSubject, decimal WaterWeightGrams);

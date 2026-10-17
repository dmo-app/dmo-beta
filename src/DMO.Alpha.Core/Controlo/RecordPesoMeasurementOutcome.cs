namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado do registo: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific da leitura persistida (measurement_id canónico,
/// posição física e os valores derivados calculados pelo backend).
/// </summary>
public sealed record RecordPesoMeasurementOutcome(
    RecordPesoMeasurementResult Result,
    RecordedPesoMeasurement? Measurement = null);

/// <summary>
/// Read model purpose-specific da leitura registada: o facto observado
/// (número/posição CM visível, massa de água na totalidade) e os valores
/// derivados do cálculo principal para esta leitura.
/// </summary>
public sealed record RecordedPesoMeasurement(
    int MeasurementId,
    int PesoId,
    int RowPosition,
    string CmSubject,
    decimal WaterWeightGrams,
    decimal CapacityCm3,
    decimal GlassWeightGrams);

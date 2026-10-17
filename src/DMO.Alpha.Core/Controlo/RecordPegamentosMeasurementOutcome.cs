namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado do registo: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific da medição persistida (measurement_id canónico,
/// posição física, os factos observados, o nominal congelado e a avaliação
/// do corredor calculada pelo backend).
/// </summary>
public sealed record RecordPegamentosMeasurementOutcome(
    RecordPegamentosMeasurementResult Result,
    RecordedPegamentosMeasurement? Measurement = null);

/// <summary>
/// Read model purpose-specific da medição registada: o facto observado
/// (componente, gesto em cruz), o nominal gargalo congelado na linha e a
/// avaliação backend contra o corredor (derivados + valores verificados).
/// </summary>
public sealed record RecordedPegamentosMeasurement(
    int MeasurementId,
    int PegamentosId,
    int? ControloId,
    int RowPosition,
    PegamentosComponent Component,
    decimal CosturaMm,
    decimal? ContraCosturaMm,
    decimal? NominalGargaloMm,
    PegamentosRowEvaluation Evaluation);

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
    PegamentosRowEvaluation Evaluation)
{
    /// <summary>
    /// True quando esta leitura começou uma folha nova numa produção que já
    /// tinha outra: a CM, a BQ ou o MF mudou e a folha anterior ficou como
    /// estava (18/10/2026).
    /// </summary>
    public bool StartedNewSheet { get; init; }
}

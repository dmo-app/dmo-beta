namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Handler do write path das medições de Pegamentos production-scoped.
/// </summary>
public interface IRecordPegamentosMeasurementHandler
{
    Task<RecordPegamentosMeasurementOutcome> HandleAsync(
        RecordPegamentosMeasurement command,
        CancellationToken cancellationToken = default);
}

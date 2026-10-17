namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Handler do comando de registo de uma leitura de Peso num peso_id
/// existente. A leitura é acrescentada ao Peso como facto histórico — nunca
/// reescrita, compensada nem apagada — e não altera o Peso, as Tools nem
/// qualquer contexto (que neste corte não existe). O cálculo da leitura é
/// produzido pelo backend com os inputs consumidos persistidos no Peso.
/// </summary>
public interface IRecordPesoMeasurementHandler
{
    Task<RecordPesoMeasurementOutcome> HandleAsync(
        RecordPesoMeasurement command,
        CancellationToken cancellationToken = default);
}

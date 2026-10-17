namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado do registo de uma leitura de Peso.
/// </summary>
public enum RecordPesoMeasurementResult
{
    Success,

    /// <summary>O peso_id não existe.</summary>
    PesoNotFound
}

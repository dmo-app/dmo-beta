namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Corrigir ou apagar uma leitura do Peso (Diogo, 27/10/2026: «não consigo
/// editar ou apagar»; Decisão 001, como nos Pegamentos). A leitura é procurada
/// dentro do Peso aberto (peso_id). Um Peso de antes de uma troca da CM ou da
/// BQ também aceita correções (resposta R16-3 do especialista).
/// </summary>
public sealed record PesoMeasurementAnchor(int MeasurementId, int PesoId);

/// <summary>A correção do N.º CM e da massa de água de uma leitura.</summary>
public sealed record EditPesoMeasurement(
    PesoMeasurementAnchor Anchor,
    string? CmSubject,
    decimal WaterWeightGrams);

public enum CorrectPesoMeasurementResult
{
    Success,

    /// <summary>A leitura não existe neste Peso: nada foi alterado.</summary>
    MeasurementNotFound,

    /// <summary>
    /// Com esta massa de água a capacidade ou o peso do vidro dão 0 ou menos:
    /// fisicamente impossível, como no registo. Nada foi alterado.
    /// </summary>
    ResultNonPositive
}

public interface ICorrectPesoMeasurementHandler
{
    Task<CorrectPesoMeasurementResult> EditAsync(
        EditPesoMeasurement command,
        CancellationToken cancellationToken = default);

    /// <summary>Apaga a leitura e renumera as restantes do Peso (1..n), pela ordem de registo.</summary>
    Task<CorrectPesoMeasurementResult> DeleteAsync(
        PesoMeasurementAnchor anchor,
        CancellationToken cancellationToken = default);
}

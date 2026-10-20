namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Corrigir ou apagar uma leitura dos Pegamentos (Diogo, 17/10/2026). A
/// leitura é procurada dentro da folha aberta: pelo controlo_id da produção
/// ou, sem Job On, pelo pegamentos_id.
/// </summary>
public sealed record PegamentosMeasurementAnchor(int MeasurementId, int? ControloId, int? PegamentosId);

/// <summary>A correção da costura e da contra costura de uma leitura; o nominal congelado mantém-se.</summary>
public sealed record EditPegamentosMeasurement(
    PegamentosMeasurementAnchor Anchor,
    decimal CosturaMm,
    decimal? ContraCosturaMm);

public enum CorrectPegamentosMeasurementResult
{
    Success,

    /// <summary>A leitura não existe nesta folha: nada foi alterado.</summary>
    MeasurementNotFound,

    /// <summary>
    /// A costura ou a contra costura é 0 ou menos: fisicamente impossível
    /// (Diogo, 18/10/2026). Nada foi alterado.
    /// </summary>
    ImpossibleValue
}

public interface ICorrectPegamentosMeasurementHandler
{
    Task<CorrectPegamentosMeasurementResult> EditAsync(
        EditPegamentosMeasurement command,
        CancellationToken cancellationToken = default);

    /// <summary>Apaga a leitura e renumera as restantes da folha (1..n), pela ordem de registo.</summary>
    Task<CorrectPegamentosMeasurementResult> DeleteAsync(
        PegamentosMeasurementAnchor anchor,
        CancellationToken cancellationToken = default);
}

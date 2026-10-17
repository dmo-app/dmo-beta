namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O comando de criação de um registo de comparação de pesos: as novas
/// leituras dos contra-moldes contra o Peso inicial de uma produção. A
/// validação e o cálculo são da MESMA fonte da comparação ao vivo (a
/// <see cref="IPesoComparisonQuery.CompararLeiturasAsync"/> — fonte única);
/// o handler persiste o registo com os inputs consumidos CONGELADOS.
/// </summary>
public sealed record CreatePesoComparacao(
    int PesoInicialId,
    string? CmToolId,
    decimal WaterTemperatureC,
    IReadOnlyList<PesoComparacaoLeituraInput> Leituras,
    string? Note = null);

public sealed record CreatePesoComparacaoOutcome(
    PesoComparacaoResultado Result,
    PesoComparacaoReadModel? Comparacao = null,
    IReadOnlyList<PesoComparacaoLeituraIgnorada>? Ignoradas = null);

public interface ICreatePesoComparacaoHandler
{
    Task<CreatePesoComparacaoOutcome> HandleAsync(
        CreatePesoComparacao command,
        CancellationToken cancellationToken = default);
}

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// A produção anterior de um Peso (o Owner 2026-10-17): o contexto vem do
/// Job On anterior (referência, número, máquina, início e fim) e o peso é
/// o que a produção usou, tirado do SAP e escrito à mão. O delta é o peso
/// médio do vidro atual menos esse peso.
/// </summary>
public sealed record PesoProducaoAnteriorReadModel(
    int? JobOnId,
    string? Reference,
    string? ProductionNumber,
    string? Machine,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    decimal? PesoGramas)
{
    /// <summary>Peso médio atual menos o peso da produção anterior, em g e em %.</summary>
    public (decimal? Grams, decimal? Percent) Delta(decimal? pesoMedioAtual) =>
        PesoCalculation.NominalDeviation(pesoMedioAtual, PesoGramas);
}

/// <summary>Uma produção anterior que se pode escolher (mesma referência, outro Job On).</summary>
public sealed record PesoProducaoAnteriorOpcao(
    int JobOnId,
    string ProductionNumber,
    string Machine,
    DateOnly? InicioEm,
    DateOnly? FimEm);

public enum GuardarProducaoAnteriorResult
{
    Success,
    PesoNotFound,
    JobOnNotFound,
    PesoInvalido
}

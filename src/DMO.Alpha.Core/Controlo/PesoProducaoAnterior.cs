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
    /// <summary>O controlo da produção escolhida (null sem escolha): a Revisão compara com ele (C6).</summary>
    public int? ControloId { get; init; }

    /// <summary>
    /// O que estava guardado quando a página abriu (audit externo, 16/11/2026,
    /// DMO-760): ao gravar, se outra pessoa já mudou a produção anterior, não
    /// se escreve por cima sem aviso.
    /// </summary>
    public string Versao => VersaoDe(ControloId, PesoGramas);

    public static string VersaoDe(int? controloId, decimal? pesoGramas) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{controloId}|{pesoGramas}");

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
    PesoInvalido,

    /// <summary>Outra pessoa gravou a produção anterior depois de a página abrir: nada foi gravado.</summary>
    MudouEntretanto
}

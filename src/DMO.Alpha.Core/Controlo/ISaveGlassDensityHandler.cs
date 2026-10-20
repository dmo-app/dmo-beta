using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Gravação da densidade do vidro NNPB e PS nas Definições do Controlo (o
/// Diogo, 19/10/2026, D15: «nas Definições deve haver densidade de vidro
/// NNPB e PS, e campos para alterar»). Só os Pesos criados depois usam o
/// valor novo: cada Peso congela a densidade com que foi criado.
/// </summary>
public interface ISaveGlassDensityHandler
{
    Task<SaveGlassDensityResult> SaveAsync(
        decimal nnpbGramsPerCm3,
        decimal psGramsPerCm3,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que a gravação fez. Como a tolerância dos Pegamentos, a densidade nunca
/// é recusada por estar fora do habitual (avisos em vez de bloqueios, Diogo
/// 19/10/2026): grava com aviso, e com mais de quatro casas decimais grava
/// arredondada, com aviso. Só zero ou negativo não grava, porque não é uma
/// densidade (o peso do vidro dava zero ou negativo); nesse caso nenhum dos
/// dois valores é gravado.
/// </summary>
public sealed record SaveGlassDensityResult(
    bool Saved,
    IReadOnlyList<CmProcess> Rounded,
    IReadOnlyList<CmProcess> OutsideUsualRange)
{
    public static SaveGlassDensityResult NotPositive { get; } = new(false, [], []);
}

/// <summary>
/// O habitual para a densidade do vidro (g/cm³), com quatro casas decimais
/// (as da fábrica: NNPB 2,4027 e PS 2,4231). Fora disto grava na mesma, com
/// aviso.
/// </summary>
public static class GlassDensityLimits
{
    public const decimal Minimum = 2.0000m;
    public const decimal Maximum = 3.0000m;

    public static bool IsUsual(decimal density) => density >= Minimum && density <= Maximum;

    /// <summary>A densidade como fica gravada: quatro casas decimais.</summary>
    public static decimal Normalize(decimal density) =>
        decimal.Round(density, 4, MidpointRounding.AwayFromZero);
}

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Gravação da tolerância do corredor de Pegamentos nas Definições (o
/// proprietário 17/10/2026: «a tolerância pode ficar nas definições. A
/// norma é 0.2 mas se alguém quiser outra poder alterar»). Só afeta os
/// registos criados depois: cada registo congela a tolerância com que foi
/// criado.
/// </summary>
public interface ISavePegamentosToleranceHandler
{
    Task<SavePegamentosToleranceResult> SaveAsync(
        decimal toleranceMm,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que a gravação fez. A tolerância nunca é recusada (Diogo, 19/10/2026:
/// «tolerância é escolhida nas definições, está 0.2, nunca bloqueia»; os
/// Pegamentos servem para ver as cotas fora de tolerância): fora do habitual
/// grava com aviso, com mais de duas casas decimais grava arredondada, com
/// aviso. Só um valor negativo não grava, porque não é uma tolerância (o
/// corredor ficava ao contrário).
/// </summary>
public sealed record SavePegamentosToleranceResult(
    bool Saved,
    decimal ToleranceMm,
    bool Rounded,
    bool OutsideUsualRange)
{
    public static SavePegamentosToleranceResult Negative(decimal toleranceMm) =>
        new(false, toleranceMm, false, false);
}

/// <summary>
/// O habitual para a tolerância (mm), com duas casas decimais. Fora disto
/// a tolerância grava na mesma, com aviso.
/// </summary>
public static class PegamentosToleranceLimits
{
    public const decimal MinimumMm = 0.01m;
    public const decimal MaximumMm = 2.00m;

    public static bool IsUsual(decimal toleranceMm) =>
        toleranceMm >= MinimumMm && toleranceMm <= MaximumMm;

    /// <summary>A tolerância como fica gravada: duas casas decimais.</summary>
    public static decimal Normalize(decimal toleranceMm) =>
        decimal.Round(toleranceMm, 2, MidpointRounding.AwayFromZero);
}

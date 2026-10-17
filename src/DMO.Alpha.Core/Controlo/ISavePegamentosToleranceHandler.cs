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

public enum SavePegamentosToleranceResult
{
    Success,
    OutOfRange
}

/// <summary>Os limites aceites para a tolerância (mm), com duas casas decimais.</summary>
public static class PegamentosToleranceLimits
{
    public const decimal MinimumMm = 0.01m;
    public const decimal MaximumMm = 2.00m;

    public static bool IsValid(decimal toleranceMm) =>
        toleranceMm >= MinimumMm
        && toleranceMm <= MaximumMm
        && decimal.Round(toleranceMm, 2) == toleranceMm;
}

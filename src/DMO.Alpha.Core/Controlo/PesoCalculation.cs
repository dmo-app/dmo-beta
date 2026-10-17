namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O cálculo do Peso detido pela aplicação (backend/Core) — o blueprint
/// proíbe o frontend de manter uma implementação concorrente deste cálculo
/// industrial. O TP/calote não é termo matemático adicional desta fórmula
/// (o cálculo de suporte TP é separado e não está neste corte):
/// <code>
/// capacity = water weight / water density
/// glass weight = (capacity + volume_marisa - volume_puncao) × glass density
/// </code>
/// </summary>
public static class PesoCalculation
{
    /// <summary>
    /// capacity (cm³) = massa de água medida (g) / densidade da água (g/cm³)
    /// resolvida pela tabela canónica para o grau inteiro da temperatura.
    /// </summary>
    public static decimal CapacityCm3(decimal waterWeightGrams, decimal waterDensityGramsPerCm3) =>
        waterWeightGrams / waterDensityGramsPerCm3;

    /// <summary>
    /// Peso do vidro (g) = (capacity + volume_marisa - volume_puncao) ×
    /// densidade do vidro. Os volumes e a densidade são os valores consumidos
    /// (preservados no Peso como evidência histórica).
    /// </summary>
    public static decimal GlassWeightGrams(
        decimal capacityCm3,
        decimal volumeMarisaCm3,
        decimal volumePuncaoCm3,
        decimal glassDensityGramsPerCm3) =>
        (capacityCm3 + volumeMarisaCm3 - volumePuncaoCm3) * glassDensityGramsPerCm3;
    /// <summary>Informative difference from drawing weight, never a tolerance decision.</summary>
    public static (decimal? Grams, decimal? Percent) NominalDeviation(decimal? current, decimal? nominal)
    {
        if (current is not { } c || nominal is not { } n) return (null, null);
        return (c - n, n == 0 ? null : (c - n) / n * 100);
    }
}

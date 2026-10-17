namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado da criação: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do Peso criado (peso_id canónico emitido pelo
/// backend, âncoras de Tool e os valores consumidos congelados).
/// </summary>
public sealed record CreateToolScopedPesoOutcome(
    CreateToolScopedPesoResult Result,
    CreatedPeso? Peso = null);

/// <summary>
/// Read model purpose-specific do Peso tool-scoped criado.
/// </summary>
public sealed record CreatedPeso(
    int PesoId,
    string CmToolId,
    string BqToolId,
    string Process,
    int WaterTemperatureC,
    decimal WaterDensityGramsPerCm3,
    decimal GlassDensityGramsPerCm3,
    decimal VolumeMarisaCm3,
    decimal VolumePuncaoCm3,
    decimal? NominalWeightGrams);

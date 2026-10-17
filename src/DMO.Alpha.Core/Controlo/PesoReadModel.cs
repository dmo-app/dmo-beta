namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Read model purpose-specific do Peso tool-scoped: as âncoras canónicas
/// (peso_id, tool_ids), os valores consumidos congelados como evidência
/// histórica (processo, grau inteiro da temperatura da água, densidades e
/// volumes), os factos descritivos atuais das Tools (apenas leitura — a
/// composição não transfere ownership), as leituras persistidas com os
/// valores derivados do cálculo principal e as médias (null quando o Peso
/// ainda não tem leituras — em branco, nunca zero exibido como facto).
/// </summary>
public sealed record PesoReadModel(
    int PesoId,
    string CmToolId,
    string? CmToolReference,
    string? CmToolLot,
    string BqToolId,
    string? BqToolReference,
    string? BqToolLot,
    string Process,
    int WaterTemperatureC,
    decimal WaterDensityGramsPerCm3,
    decimal GlassDensityGramsPerCm3,
    decimal VolumeMarisaCm3,
    decimal VolumePuncaoCm3,
    decimal? NominalWeightGrams,
    IReadOnlyList<PesoMeasurementReadModel> Measurements,
    PesoAveragesReadModel? Averages,
    int? ControloId = null);

/// <summary>
/// Uma leitura persistida do Peso tal como o surface Peso a apresenta: o
/// facto observado (posição física, número/posição CM visível, massa de água
/// na totalidade) e os valores derivados do cálculo principal para a leitura
/// (capacity e peso do vidro), calculados a partir dos inputs consumidos
/// persistidos no Peso.
/// </summary>
public sealed record PesoMeasurementReadModel(
    int MeasurementId,
    int RowPosition,
    string CmSubject,
    decimal WaterWeightGrams,
    decimal CapacityCm3,
    decimal GlassWeightGrams);

/// <summary>
/// Médias das leituras do Peso (massa de água, capacidade e peso do vidro),
/// derivadas apenas das leituras persistidas e dos inputs consumidos.
/// </summary>
public sealed record PesoAveragesReadModel(
    decimal AverageWaterWeightGrams,
    decimal AverageCapacityCm3,
    decimal AverageGlassWeightGrams);

/// <summary>
/// Item do histórico de Pesos de uma Tool: o peso_id canónico, as âncoras
/// (BQ usada, processo, grau da temperatura), o número de leituras e a média
/// do peso do vidro calculada a partir dos dados persistidos (null quando o
/// Peso ainda não tem leituras).
/// </summary>
public sealed record PesoToolHistoryItem(
    int PesoId,
    string BqToolId,
    string? BqToolReference,
    string? BqToolLot,
    string Process,
    int WaterTemperatureC,
    decimal GlassDensityGramsPerCm3,
    int MeasurementCount,
    decimal? AverageGlassWeightGrams);

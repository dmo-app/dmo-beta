using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Serviço de leitura do surface Peso: resolução cirúrgica por peso_id
/// canónico e histórico de Pesos por tool_id (sem scans globais, projeções
/// mínimas, AsNoTracking). Os valores derivados (capacidade, peso do vidro,
/// médias) são calculados a partir dos inputs consumidos persistidos no Peso
/// e das leituras persistidas — nunca a partir do estado corrente das Tools
/// ou da configuração — para que a leitura histórica permaneça reproduzível.
/// Referência/lote usam os factos congelados do Peso, com fallback para a
/// Tool atual apenas nos campos legados sem valor preservado.
/// </summary>
public sealed class PesoQuery : IPesoQuery
{
    private readonly DmoDbContext _db;

    public PesoQuery(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// O Peso baseline do controlo de uma produção (corte C): a associação
    /// é 1:1 (o índice único do ControloId garante); null quando o controlo
    /// ainda não tem o seu Peso. A densidade da água resolve-se pela tabela
    /// canónica para o grau congelado (a decisão Owner 1b).
    /// </summary>
    public Task<PesoReadModel?> FindByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        FindAsync(_db.Pesos.Where(p => p.ControloId == controloId), cancellationToken);

    public Task<PesoReadModel?> FindPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default) =>
        FindAsync(_db.Pesos.Where(p => p.Id == pesoId), cancellationToken);

    private async Task<PesoReadModel?> FindAsync(
        IQueryable<DMO.Alpha.Core.Controlo.Peso> source,
        CancellationToken cancellationToken)
    {
        // O Peso, a referência/lote atuais das Tools (fallback dos campos
        // legados sem identificação congelada) e as leituras numa só consulta.
        var peso = await (
            from p in source.AsNoTracking()
            join cm in _db.Tools on p.CmToolId equals cm.ToolId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join bq in _db.Tools on p.BqToolId equals bq.ToolId into bqGroup
            from bq in bqGroup.DefaultIfEmpty()
            select new
            {
                p.Id,
                p.CmToolId,
                p.CmToolReferenceFrozen,
                p.CmToolLotFrozen,
                p.BqToolId,
                p.BqToolReferenceFrozen,
                p.BqToolLotFrozen,
                p.Process,
                p.WaterTemperatureC,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.NominalWeightGrams,
                p.GlassDensityGramsPerCm3,
                p.ControloId,
                CmToolReference = cm != null ? cm.Reference : null,
                CmToolLot = cm != null ? cm.Lot : null,
                BqToolReference = bq != null ? bq.Reference : null,
                BqToolLot = bq != null ? bq.Lot : null,
                Measurements = p.Measurements
                    .OrderBy(m => m.RowPosition)
                    .Select(m => new
                    {
                        m.Id,
                        m.RowPosition,
                        m.CmSubject,
                        m.WaterWeightGrams
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (peso is null)
        {
            return null;
        }

        var measurements = peso.Measurements;

        // Cálculo: os inputs consumidos congelados no Peso + as leituras
        // persistidas. A densidade da água resolve pela tabela canónica para
        // o grau congelado (invariante validada na criação).
        var resolvedWaterDensity = WaterDensityTable.Resolve(peso.WaterTemperatureC)
            ?? throw new InvalidOperationException(
                $"O grau de temperatura da água congelado no Peso {peso.Id} " +
                "está fora do intervalo suportado pela tabela canónica de densidade da água.");

        PesoAveragesReadModel? averages = null;
        var measurementReadModels = new List<PesoMeasurementReadModel>(measurements.Count);
        if (measurements.Count > 0)
        {
            decimal totalWater = 0m;
            decimal totalCapacity = 0m;
            decimal totalGlassWeight = 0m;

            foreach (var measurement in measurements)
            {
                var capacity = PesoCalculation.CapacityCm3(
                    measurement.WaterWeightGrams,
                    resolvedWaterDensity);
                var glassWeight = PesoCalculation.GlassWeightGrams(
                    capacity,
                    peso.VolumeMarisaCm3,
                    peso.VolumePuncaoCm3,
                    peso.GlassDensityGramsPerCm3);

                measurementReadModels.Add(new PesoMeasurementReadModel(
                    measurement.Id,
                    measurement.RowPosition,
                    measurement.CmSubject,
                    measurement.WaterWeightGrams,
                    capacity,
                    glassWeight));

                totalWater += measurement.WaterWeightGrams;
                totalCapacity += capacity;
                totalGlassWeight += glassWeight;
            }

            averages = new PesoAveragesReadModel(
                totalWater / measurements.Count,
                totalCapacity / measurements.Count,
                totalGlassWeight / measurements.Count);
        }

        return new PesoReadModel(
            peso.Id,
            peso.CmToolId,
            peso.CmToolReferenceFrozen ?? peso.CmToolReference,
            peso.CmToolLotFrozen ?? peso.CmToolLot,
            peso.BqToolId,
            peso.BqToolReferenceFrozen ?? peso.BqToolReference,
            peso.BqToolLotFrozen ?? peso.BqToolLot,
            peso.Process,
            peso.WaterTemperatureC,
            resolvedWaterDensity,
            peso.GlassDensityGramsPerCm3,
            peso.VolumeMarisaCm3,
            peso.VolumePuncaoCm3,
            peso.NominalWeightGrams,
            measurementReadModels,
            averages,
            peso.ControloId);
    }

    public async Task<IReadOnlyList<PesoToolHistoryItem>> ListByToolAsync(
        string toolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return Array.Empty<PesoToolHistoryItem>();
        }

        // Pesos da Tool, do mais recente para o mais antigo, com os inputs
        // consumidos congelados. Os factos descritivos da BQ usada entram
        // como composição de leitura (a BQ pode ter linhas de histórico
        // diferentes entre Pesos da mesma Tool).
        var pesos = await _db.Pesos
            .AsNoTracking()
            .Where(p => p.CmToolId == toolId)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.BqToolId,
                p.BqToolReferenceFrozen,
                p.BqToolLotFrozen,
                p.Process,
                p.WaterTemperatureC,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.GlassDensityGramsPerCm3
            })
            .ToListAsync(cancellationToken);

        if (pesos.Count == 0)
        {
            return Array.Empty<PesoToolHistoryItem>();
        }

        var bqToolIds = pesos.Select(p => p.BqToolId).Distinct().ToList();
        var bqTools = await _db.Tools
            .AsNoTracking()
            .Where(t => bqToolIds.Contains(t.ToolId))
            .ToDictionaryAsync(t => t.ToolId, t => new { t.Reference, t.Lot }, cancellationToken);

        var items = new List<PesoToolHistoryItem>(pesos.Count);
        foreach (var peso in pesos)
        {
            var (measurementCount, averageGlassWeight) = await ResolveMeasurementSummaryAsync(
                peso.Id,
                peso.WaterTemperatureC,
                peso.VolumeMarisaCm3,
                peso.VolumePuncaoCm3,
                peso.GlassDensityGramsPerCm3,
                cancellationToken);

            var bqTool = bqTools.GetValueOrDefault(peso.BqToolId);

            items.Add(new PesoToolHistoryItem(
                peso.Id,
                peso.BqToolId,
                peso.BqToolReferenceFrozen ?? bqTool?.Reference,
                peso.BqToolLotFrozen ?? bqTool?.Lot,
                peso.Process,
                peso.WaterTemperatureC,
                peso.GlassDensityGramsPerCm3,
                measurementCount,
                averageGlassWeight));
        }

        return items;
    }

    /// <summary>
    /// Resumo de um Peso para o histórico: número de leituras persistidas e
    /// a média do peso do vidro calculada a partir dos dados persistidos
    /// (null quando o Peso ainda não tem leituras — em branco, nunca zero
    /// exibido como facto).
    /// </summary>
    private async Task<(int MeasurementCount, decimal? AverageGlassWeightGrams)> ResolveMeasurementSummaryAsync(
        int pesoId,
        int waterTemperatureC,
        decimal volumeMarisaCm3,
        decimal volumePuncaoCm3,
        decimal glassDensityGramsPerCm3,
        CancellationToken cancellationToken)
    {
        var waterWeights = await _db.PesoMeasurements
            .AsNoTracking()
            .Where(m => m.PesoId == pesoId)
            .OrderBy(m => m.RowPosition)
            .Select(m => m.WaterWeightGrams)
            .ToListAsync(cancellationToken);

        if (waterWeights.Count == 0)
        {
            return (0, null);
        }

        var resolvedWaterDensity = WaterDensityTable.Resolve(waterTemperatureC)
            ?? throw new InvalidOperationException(
                $"O grau de temperatura da água congelado no Peso {pesoId} " +
                "está fora do intervalo suportado pela tabela canónica de densidade da água.");

        decimal totalGlassWeight = 0m;
        foreach (var waterWeight in waterWeights)
        {
            var capacity = PesoCalculation.CapacityCm3(waterWeight, resolvedWaterDensity);
            totalGlassWeight += PesoCalculation.GlassWeightGrams(
                capacity,
                volumeMarisaCm3,
                volumePuncaoCm3,
                glassDensityGramsPerCm3);
        }

        return (waterWeights.Count, totalGlassWeight / waterWeights.Count);
    }
}

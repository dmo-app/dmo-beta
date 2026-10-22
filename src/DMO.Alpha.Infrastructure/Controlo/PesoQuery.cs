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
    /// O Peso do controlo de uma produção para um valor só (corte C): o da
    /// CM e da BQ que a produção tem agora ou, depois de uma troca sem Peso
    /// novo ainda, o mais recente; null quando o controlo ainda não tem Peso.
    /// A densidade da água resolve-se pela tabela canónica para o grau
    /// congelado (a decisão Owner 1b).
    /// </summary>
    public async Task<PesoReadModel?> FindByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var pesos = await ListByControloAsync(controloId, cancellationToken);
        return pesos.FirstOrDefault(p => p.IsCurrent) ?? pesos.LastOrDefault();
    }

    /// <summary>
    /// O Peso da CM e da BQ que a produção tem agora; null antes do primeiro
    /// e depois de uma troca da CM ou da BQ, até o Peso novo ser criado
    /// (respostas R15 e R16-5 do especialista, 18/10/2026).
    /// </summary>
    public async Task<PesoReadModel?> FindCurrentByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        (await ListByControloAsync(controloId, cancellationToken)).FirstOrDefault(p => p.IsCurrent);

    /// <summary>
    /// Os Pesos do controlo pela ordem de gravação (um por CM+BQ), cada um
    /// com as suas ferramentas congeladas; o atual é o da combinação que a
    /// produção tem agora (o mais antigo, se dois ficaram com a mesma ao
    /// apagar ferramentas).
    /// </summary>
    public async Task<IReadOnlyList<PesoReadModel>> ListByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var pesos = await FindManyAsync(_db.Pesos.Where(p => p.ControloId == controloId), cancellationToken);
        if (pesos.Count == 0)
        {
            return pesos;
        }

        var current = await PesoCombinations.CurrentCombinationAsync(_db, controloId, cancellationToken);
        var currentId = current is null
            ? (int?)null
            : pesos.FirstOrDefault(p => current.Matches(p.CmToolId, p.BqToolId))?.PesoId;
        return pesos.Select(p => p with { IsCurrent = p.PesoId == currentId }).ToList();
    }

    public async Task<PesoReadModel?> FindPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default)
    {
        var peso = (await FindManyAsync(_db.Pesos.Where(p => p.Id == pesoId), cancellationToken)).FirstOrDefault();

        // Um Peso de uma produção que entretanto trocou a CM ou a BQ não é o
        // atual: fica como estava, sem leituras novas.
        return peso is { ControloId: int controloId }
            ? peso with { IsCurrent = await PesoCombinations.CurrentPesoIdAsync(_db, controloId, cancellationToken) == peso.PesoId }
            : peso;
    }

    public async Task<IReadOnlyList<PesoReadModel>> ListParaComparacaoAsync(
        int pesoId,
        CancellationToken cancellationToken = default) =>
        (await FindManyAsync(_db.Pesos.Where(p => p.ComparacaoDoPesoId == pesoId), cancellationToken))
            .OrderByDescending(p => p.PesoId)
            .ToList();

    private async Task<IReadOnlyList<PesoReadModel>> FindManyAsync(
        IQueryable<DMO.Alpha.Core.Controlo.Peso> source,
        CancellationToken cancellationToken)
    {
        // Os Pesos, a referência/lote atuais das Tools (fallback dos campos
        // legados sem identificação congelada) e as leituras numa só consulta,
        // pela ordem de gravação.
        var rows = await (
            from p in source.AsNoTracking()
            join cm in _db.Tools on p.CmToolId equals cm.ToolId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join bq in _db.Tools on p.BqToolId equals bq.ToolId into bqGroup
            from bq in bqGroup.DefaultIfEmpty()
            orderby p.Id
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
                p.WaterDensityGramsPerCm3,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.NominalWeightGrams,
                p.GlassDensityGramsPerCm3,
                p.ControloId,
                p.ComparacaoDoPesoId,
                p.RegistadoEmUtc,
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
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            // Cálculo: os inputs consumidos congelados no Peso + as leituras
            // persistidas. A densidade da água é a gravada no Peso (a tabela
            // canónica para o grau, num Peso sem ela).
            var resolvedWaterDensity = WaterDensityTable.ForPeso(row.WaterDensityGramsPerCm3, row.WaterTemperatureC)
                ?? throw new InvalidOperationException(
                    $"O grau de temperatura da água congelado no Peso {row.Id} " +
                    "está fora do intervalo suportado pela tabela canónica de densidade da água.");

            var measurements = row.Measurements;
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
                        row.VolumeMarisaCm3,
                        row.VolumePuncaoCm3,
                        row.GlassDensityGramsPerCm3);

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
                row.Id,
                row.CmToolId,
                row.CmToolReferenceFrozen ?? row.CmToolReference,
                row.CmToolLotFrozen ?? row.CmToolLot,
                row.BqToolId,
                row.BqToolReferenceFrozen ?? row.BqToolReference,
                row.BqToolLotFrozen ?? row.BqToolLot,
                row.Process,
                row.WaterTemperatureC,
                resolvedWaterDensity,
                row.GlassDensityGramsPerCm3,
                row.VolumeMarisaCm3,
                row.VolumePuncaoCm3,
                row.NominalWeightGrams,
                measurementReadModels,
                averages,
                row.ControloId)
            {
                RegistadoEmUtc = row.RegistadoEmUtc is { } em ? DateTime.SpecifyKind(em, DateTimeKind.Utc) : null,
                ComparacaoDoPesoId = row.ComparacaoDoPesoId
            };
        }).ToList();
    }
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o read model do surface Peso: resolução por peso_id com as leituras
/// persistidas e os valores derivados calculados pelo backend a partir dos
/// inputs consumidos congelados no Peso (nunca do estado corrente das Tools
/// ou da configuração), as médias (null sem leituras — em branco, nunca zero
/// exibido como facto) e o histórico de Pesos por tool_id com os resumos
/// calculados dos dados persistidos.
/// </summary>
public sealed class PesoQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoQuery _query;

    public PesoQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Peso> SeedPesoAsync(
        string cmToolId = "T3",
        string bqToolId = "BQ-TOOL-9",
        int temperatureC = 20,
        decimal volumeMarisa = 10m,
        decimal volumePuncao = 30m,
        decimal? nominal = 200m,
        decimal glassDensity = 2.50m)
    {
        // Tools idempotentes por tool_id: o seed pode criar vários Pesos das
        // mesmas ferramentas (cada pesagem real é um peso_id novo).
        if (!await _db.Tools.AnyAsync(t => t.ToolId == cmToolId))
        {
            _db.Tools.Add(new Tool { ToolId = cmToolId, Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        }

        if (!await _db.Tools.AnyAsync(t => t.ToolId == bqToolId))
        {
            _db.Tools.Add(new Tool { ToolId = bqToolId, Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        }

        var peso = new Peso
        {
            CmToolId = cmToolId,
            BqToolId = bqToolId,
            Process = "NNPB",
            WaterTemperatureC = temperatureC,
            VolumeMarisaCm3 = volumeMarisa,
            VolumePuncaoCm3 = volumePuncao,
            NominalWeightGrams = nominal,
            GlassDensityGramsPerCm3 = glassDensity
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        return peso;
    }

    private async Task SeedMeasurementsAsync(int pesoId, params (string Subject, decimal Water)[] rows)
    {
        var position = 1;
        foreach (var (subject, water) in rows)
        {
            _db.PesoMeasurements.Add(new PesoMeasurement
            {
                PesoId = pesoId,
                RowPosition = position++,
                CmSubject = subject,
                WaterWeightGrams = water
            });
        }

        await _db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HistoricalIdentification_UsesRecordedFacts_WithLegacyFallback(bool frozen)
    {
        var peso = await SeedPesoAsync();
        if (frozen)
        {
            peso.CmToolReferenceFrozen = "5447";
            peso.CmToolLotFrozen = "3";
            peso.BqToolReferenceFrozen = "T194";
            peso.BqToolLotFrozen = "12";
        }
        await SeedMeasurementsAsync(peso.Id, ("CM-1", 100m));
        var before = (await _query.FindPesoAsync(peso.Id))!;
        var cm = await _db.Tools.SingleAsync(t => t.ToolId == "T3");
        var bq = await _db.Tools.SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        cm.Reference = "CM-EDITADA";
        cm.Lot = "33";
        bq.Reference = "BQ-EDITADA";
        bq.Lot = "120";
        await _db.SaveChangesAsync();

        var result = (await _query.FindPesoAsync(peso.Id))!;
        Assert.Equal(frozen ? "5447" : "CM-EDITADA", result.CmToolReference);
        Assert.Equal(frozen ? "3" : "33", result.CmToolLot);
        Assert.Equal(frozen ? "T194" : "BQ-EDITADA", result.BqToolReference);
        Assert.Equal(frozen ? "12" : "120", result.BqToolLot);
        Assert.Equal(before.Averages, result.Averages);
        Assert.Equal("CM-EDITADA", cm.Reference);
        Assert.Equal("BQ-EDITADA", bq.Reference);
    }

    [Fact]
    public async Task HistoricalIdentification_PartialLegacyFacts_FallBackPerField()
    {
        var peso = await SeedPesoAsync();
        peso.CmToolReferenceFrozen = "CM-ANTIGA";
        peso.BqToolLotFrozen = "LOTE-ANTIGO";
        await _db.SaveChangesAsync();
        var result = (await _query.FindPesoAsync(peso.Id))!;
        Assert.Equal("CM-ANTIGA", result.CmToolReference);
        Assert.Equal("3", result.CmToolLot);
        Assert.Equal("T194", result.BqToolReference);
        Assert.Equal("LOTE-ANTIGO", result.BqToolLot);
    }

    [Fact]
    public async Task FindPesoAsync_UnknownPesoId_ReturnsNull()
    {
        await SeedPesoAsync();

        Assert.Null(await _query.FindPesoAsync(9999));
    }

    [Fact]
    public async Task FindPesoAsync_NoMeasurements_BlankAveragesNotZero()
    {
        // Sem leituras não há médias: null (em branco), nunca zero exibido
        // como facto.
        var peso = await SeedPesoAsync();

        var readModel = await _query.FindPesoAsync(peso.Id);

        Assert.NotNull(readModel);
        Assert.Empty(readModel!.Measurements);
        Assert.Null(readModel.Averages);
        Assert.Equal("T3", readModel.CmToolId);
        Assert.Equal("5447", readModel.CmToolReference);
        Assert.Equal("3", readModel.CmToolLot);
        Assert.Equal("BQ-TOOL-9", readModel.BqToolId);
        Assert.Equal("T194", readModel.BqToolReference);
        Assert.Equal("12", readModel.BqToolLot);
        Assert.Equal("NNPB", readModel.Process);
        Assert.Equal(20, readModel.WaterTemperatureC);
        Assert.Equal(0.99717m, readModel.WaterDensityGramsPerCm3);
        Assert.Equal(10m, readModel.VolumeMarisaCm3);
        Assert.Equal(30m, readModel.VolumePuncaoCm3);
        Assert.Equal(200m, readModel.NominalWeightGrams);
        Assert.Equal(2.50m, readModel.GlassDensityGramsPerCm3);
    }

    [Fact]
    public async Task FindPesoAsync_CalculatesRowsAndAveragesFromPersistedInputs()
    {
        // Valores exatos com a tabela real da fábrica (decisão Owner 1b):
        // 20 °C → densidade 0.99717; 199.64 / 0.99717 =
        // 200.20658463451567937262452741; 99.82 / 0.99717 =
        // 100.10329231725783968631226371; peso do vidro = (capacity + 10 - 30)
        // × 2.50 → 450.51646158628919843156131852 e 200.25823079314459921578065928;
        // médias 149.73 / 150.15493847588675952946839556 / 325.38734618971689882367098890.
        var peso = await SeedPesoAsync();
        await SeedMeasurementsAsync(
            peso.Id,
            ("12", 199.64m),
            ("21", 99.82m));

        var readModel = await _query.FindPesoAsync(peso.Id);

        Assert.NotNull(readModel);
        Assert.Equal(2, readModel!.Measurements.Count);

        var first = readModel.Measurements[0];
        Assert.Equal(1, first.RowPosition);
        Assert.Equal("12", first.CmSubject);
        Assert.Equal(199.64m, first.WaterWeightGrams);
        Assert.Equal(200.20658463451567937262452741m, first.CapacityCm3);
        Assert.Equal(450.51646158628919843156131852m, first.GlassWeightGrams);

        var second = readModel.Measurements[1];
        Assert.Equal(2, second.RowPosition);
        Assert.Equal("21", second.CmSubject);
        Assert.Equal(99.82m, second.WaterWeightGrams);
        Assert.Equal(100.10329231725783968631226371m, second.CapacityCm3);
        Assert.Equal(200.25823079314459921578065928m, second.GlassWeightGrams);

        Assert.NotNull(readModel.Averages);
        Assert.Equal(149.73m, readModel.Averages!.AverageWaterWeightGrams);
        Assert.Equal(150.15493847588675952946839556m, readModel.Averages.AverageCapacityCm3);
        Assert.Equal(325.38734618971689882367098890m, readModel.Averages.AverageGlassWeightGrams);
    }

    [Fact]
    public async Task FindPesoAsync_CalculationFrozenAgainstLaterToolEdits()
    {
        // A leitura histórica permanece reproduzível: depois de o contexto
        // técnico da Tool mudar (correção do volume_marisa) e a densidade
        // configurada mudar, o read model do Peso concluído continua a
        // calcular com os inputs consumidos congelados no Peso.
        var peso = await SeedPesoAsync(volumeMarisa: 10m, glassDensity: 2.50m);
        await SeedMeasurementsAsync(peso.Id, ("12", 199.64m));
        var pesoId = peso.Id;

        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 99m
        });
        _db.GlassDensities.Add(new GlassDensity
        {
            Process = CmProcess.Nnpb,
            DensityGramsPerCm3 = 3.00m
        });
        await _db.SaveChangesAsync();

        var readModel = await _query.FindPesoAsync(pesoId);

        Assert.NotNull(readModel);
        Assert.Equal(10m, readModel!.VolumeMarisaCm3);
        Assert.Equal(2.50m, readModel.GlassDensityGramsPerCm3);
        Assert.Single(readModel.Measurements);
        Assert.Equal(450.51646158628919843156131852m, readModel.Measurements[0].GlassWeightGrams);
        Assert.Equal(450.51646158628919843156131852m, readModel.Averages!.AverageGlassWeightGrams);
    }

    /// <summary>
    /// Uma produção que trocou a CM de T3 para T4: o Peso de T3 (o anterior,
    /// com uma leitura) e o de T4 (o atual), os dois do mesmo controlo.
    /// </summary>
    private async Task<(int ControloId, Peso Antigo, Peso Atual)> SeedTwoPesosAsync()
    {
        var antigo = await SeedPesoAsync();
        await SeedMeasurementsAsync(antigo.Id, ("12", 199.64m));
        var atual = await SeedPesoAsync(cmToolId: "T4");
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = "202601", Machine = new Machine { Code = "B1" } };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        _db.CmContexts.Add(new CmContext { ToolId = "T3", ReplacedJobOnId = jobOn.Id });
        _db.CmContexts.Add(new CmContext { ToolId = "T4", JobOnId = jobOn.Id });
        _db.BqContexts.Add(new BqContext { ToolId = "BQ-TOOL-9", JobOnId = jobOn.Id });
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        antigo.ControloId = controlo.Id;
        atual.ControloId = controlo.Id;
        await _db.SaveChangesAsync();
        return (controlo.Id, antigo, atual);
    }

    // Uma produção pode ter vários Pesos, um por CM+BQ (respostas R15 e
    // R16-5 do especialista, 18/10/2026): a lista vem pela ordem de gravação
    // e marca o Peso das ferramentas atuais.
    [Fact]
    public async Task ListByControlo_DoisPesos_OrdenadosEMarcaOAtual()
    {
        var (controloId, antigo, atual) = await SeedTwoPesosAsync();

        var pesos = await _query.ListByControloAsync(controloId);

        Assert.Equal(new[] { antigo.Id, atual.Id }, pesos.Select(p => p.PesoId).ToArray());
        Assert.False(pesos[0].IsCurrent);
        Assert.True(pesos[1].IsCurrent);
        Assert.Single(pesos[0].Measurements);
        Assert.Empty(pesos[1].Measurements);
        Assert.Equal(atual.Id, (await _query.FindCurrentByControloAsync(controloId))!.PesoId);
        Assert.Equal(atual.Id, (await _query.FindByControloAsync(controloId))!.PesoId);
        Assert.False((await _query.FindPesoAsync(antigo.Id))!.IsCurrent);
        Assert.True((await _query.FindPesoAsync(atual.Id))!.IsCurrent);
    }

    // Sem o Peso das ferramentas atuais (a troca ainda sem Peso novo), não há
    // Peso atual; a leitura única dos revisores fica com o mais recente.
    [Fact]
    public async Task FindByControlo_SemPesoAtual_DevolveOMaisRecente()
    {
        var (controloId, antigo, atual) = await SeedTwoPesosAsync();
        var bq = await _db.BqContexts.SingleAsync();
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-10", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
        bq.JobOnId = null;
        bq.ReplacedJobOnId = (await _db.Controlos.SingleAsync()).JobOnId;
        _db.BqContexts.Add(new BqContext { ToolId = "BQ-TOOL-10", JobOnId = bq.ReplacedJobOnId });
        await _db.SaveChangesAsync();

        Assert.Null(await _query.FindCurrentByControloAsync(controloId));
        Assert.Equal(atual.Id, (await _query.FindByControloAsync(controloId))!.PesoId);
        Assert.All(await _query.ListByControloAsync(controloId), p => Assert.False(p.IsCurrent));
        Assert.NotEqual(antigo.Id, atual.Id);
    }
}

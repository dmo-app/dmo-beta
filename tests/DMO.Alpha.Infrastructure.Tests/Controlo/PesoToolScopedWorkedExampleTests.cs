using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O CASO DE ACEITAÇÃO do corte tool-scoped — o exemplo trabalhado da spec
/// (Controlo Create — Peso, «Worked example — Peso before production»):
/// CM Tool T3, referência 5447, lote 3, sem Job On.
/// Ferramentas → abrir T3 → criar o Peso P30 → registar as leituras reais →
/// P30 permanece diretamente consultável a partir de T3. Nenhuma
/// identidade de produção (jobon_id/controlo_id/cm_id) é inventada ou
/// exigida: o Peso é um peso_id normal associado à Tool real enquanto não
/// está associado a uma produção.
/// </summary>
public sealed class PesoToolScopedWorkedExampleTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateToolScopedPesoHandler _createHandler;
    private readonly RecordPesoMeasurementHandler _recordHandler;
    private readonly PesoQuery _pesoQuery;
    private readonly ToolLookupQuery _toolLookup;

    public PesoToolScopedWorkedExampleTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _createHandler = new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db));
        _recordHandler = new RecordPesoMeasurementHandler(_db);
        _pesoQuery = new PesoQuery(_db);
        _toolLookup = new ToolLookupQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task WorkedExample_T3_NoJobOn_P30ConsultableFromT3()
    {
        // --- Pré-condições do exemplo: a CM Tool T3 (referência 5447, lote
        //     3) e a BQ (marisa) da pesagem estão registadas no registo
        //     canónico Ferramentas; a densidade do vidro do processo NNPB
        //     está configurada. A T3 tem o processo registado; os restantes
        //     valores técnicos estão em falta e são completados no fluxo.
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb
        });
        _db.GlassDensities.Add(new GlassDensity
        {
            Process = CmProcess.Nnpb,
            DensityGramsPerCm3 = 2.50m
        });
        await _db.SaveChangesAsync();

        // --- «no Job On exists»: nenhuma produção, contexto ou controlo.
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());

        // --- Ferramentas → open T3: o tool_id resolve a Tool canónica no
        //     registo (factos descritivos da pesagem em curso).
        var openedTool = await _toolLookup.FindByToolIdAsync("T3");
        Assert.NotNull(openedTool);
        Assert.Equal(ToolType.Cm, openedTool!.Type);
        Assert.Equal("5447", openedTool.Reference);
        Assert.Equal("3", openedTool.Lot);

        // --- create Peso P30: a pesagem real com a BQ da pesagem, a
        //     temperatura medida 20.4 °C (o lookup usa o grau 20) e os
        //     valores técnicos em falta completados no fluxo — gravados no
        //     tool_id owner (regra Ferramentas), nunca como facto do Peso.
        var created = await _createHandler.HandleAsync(new CreateToolScopedPeso(
            "T3",
            "BQ-TOOL-9",
            20.4m,
            CmVolumePuncaoCm3: 30m,
            CmNominalWeightGrams: 200m,
            BqVolumeMarisaCm3: 10m));

        Assert.Equal(CreateToolScopedPesoResult.Success, created.Result);
        Assert.NotNull(created.Peso);
        var pesoId = created.Peso!.PesoId; // «P30»

        // Os inputs consumidos por P30: grau 20 (densidade da água 0.99717 —
        // tabela real da fábrica, decisão Owner 1b de 2026-10-10),
        // processo NNPB (densidade do vidro 2.50), volume_marisa 10 da BQ e
        // volume_puncao 30 da CM, nominal opcional 200.
        Assert.Equal(20, created.Peso.WaterTemperatureC);
        Assert.Equal(0.99717m, created.Peso.WaterDensityGramsPerCm3);
        Assert.Equal("NNPB", created.Peso.Process);
        Assert.Equal(2.50m, created.Peso.GlassDensityGramsPerCm3);
        Assert.Equal(10m, created.Peso.VolumeMarisaCm3);
        Assert.Equal(30m, created.Peso.VolumePuncaoCm3);
        Assert.Equal(200m, created.Peso.NominalWeightGrams);

        // A completion gravou nos tool_ids owners (reutilizável por futuros
        // Pesos) e nenhuma identidade de produção foi criada.
        var t3Technical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "T3");
        Assert.Equal(CmProcess.Nnpb, t3Technical.Process);
        Assert.Equal(30m, t3Technical.VolumePuncaoCm3);
        Assert.Equal(200m, t3Technical.NominalWeightGrams);

        var bqTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        Assert.Equal(10m, bqTechnical.VolumeMarisaCm3);

        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());

        // --- record the real readings: as leituras físicas reais de P30
        //     (massas de água observadas), acrescentadas como factos
        //     históricos com o cálculo detido pelo backend.
        var reading1 = await _recordHandler.HandleAsync(new RecordPesoMeasurement(
            pesoId, "12", 199.64m));
        var reading2 = await _recordHandler.HandleAsync(new RecordPesoMeasurement(
            pesoId, "21", 99.82m));

        Assert.Equal(RecordPesoMeasurementResult.Success, reading1.Result);
        Assert.Equal(RecordPesoMeasurementResult.Success, reading2.Result);

        // Cálculo exato por leitura com a tabela real da fábrica:
        // capacity = massa / 0.99717 (199.64 → 200.20658463451567937262452741;
        // 99.82 → 100.10329231725783968631226371); peso do vidro =
        // (capacity + 10 - 30) × 2.50 (450.51646158628919843156131852 e
        // 200.25823079314459921578065928).
        Assert.Equal(200.20658463451567937262452741m, reading1.Measurement!.CapacityCm3);
        Assert.Equal(450.51646158628919843156131852m, reading1.Measurement.GlassWeightGrams);
        Assert.Equal(100.10329231725783968631226371m, reading2.Measurement!.CapacityCm3);
        Assert.Equal(200.25823079314459921578065928m, reading2.Measurement.GlassWeightGrams);

        // O read model completo de P30: contexto consumido, leituras com os
        // valores derivados e as médias (média em água 149.73, capacidade
        // média 150.15493847588675952946839556, peso médio do vidro
        // 325.38734618971689882367098890 — recomputadas à leitura com a
        // tabela real da fábrica, decisão Owner 1b sem congelamento).
        var readModel = await _pesoQuery.FindPesoAsync(pesoId);
        Assert.NotNull(readModel);
        Assert.Equal("T3", readModel!.CmToolId);
        Assert.Equal("5447", readModel.CmToolReference);
        Assert.Equal("3", readModel.CmToolLot);
        Assert.Equal("BQ-TOOL-9", readModel.BqToolId);
        Assert.Equal(0.99717m, readModel.WaterDensityGramsPerCm3);
        Assert.Equal(2, readModel.Measurements.Count);
        Assert.Equal(450.51646158628919843156131852m, readModel.Measurements[0].GlassWeightGrams);
        Assert.Equal(200.25823079314459921578065928m, readModel.Measurements[1].GlassWeightGrams);
        Assert.NotNull(readModel.Averages);
        Assert.Equal(149.73m, readModel.Averages!.AverageWaterWeightGrams);
        Assert.Equal(150.15493847588675952946839556m, readModel.Averages.AverageCapacityCm3);
        Assert.Equal(325.38734618971689882367098890m, readModel.Averages.AverageGlassWeightGrams);

        // «At this point P30 is a normal Peso for the real Tool. No
        // production identity is fabricated merely because the measurement
        // already exists.» — o Peso existe e é consultável sem qualquer
        // produção persistida.
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());
    }
}

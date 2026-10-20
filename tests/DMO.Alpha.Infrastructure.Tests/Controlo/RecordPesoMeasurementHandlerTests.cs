using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o write path do registo de leituras reais (measurement rows): a
/// leitura é acrescentada ao peso_id existente como facto histórico — a
/// massa de água observada é registada na totalidade, nunca reescrita — a
/// posição física é atribuída pela ordem de registo, os valores derivados
/// (capacity e peso do vidro) são calculados pelo backend com os inputs
/// consumidos congelados no Peso, e o registo não altera o Peso nem as
/// Tools. Só o fisicamente impossível (peso do vidro 0 ou menos) é
/// recusado.
/// </summary>
public sealed class RecordPesoMeasurementHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly RecordPesoMeasurementHandler _handler;

    public RecordPesoMeasurementHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RecordPesoMeasurementHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// Semeia um Peso tool-scoped completo com inputs exatos: 20 °C
    /// (densidade da água 0.99717 — tabela real da fábrica, decisão Owner 1b),
    /// volume_marisa 10, volume_puncao 30, densidade do vidro 2.50 — os
    /// valores que fecham cálculos exatos (199.64 g → capacity
    /// 200.20658463451567937262452741; peso do vidro 450.51646158628919843156131852).
    /// </summary>
    private async Task<Peso> SeedPesoAsync()
    {
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            VolumePuncaoCm3 = 30m,
            NominalWeightGrams = 200m
        });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 10m
        });
        _db.GlassDensities.Add(new GlassDensity
        {
            Process = CmProcess.Nnpb,
            DensityGramsPerCm3 = 2.50m
        });

        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            NominalWeightGrams = 200m,
            GlassDensityGramsPerCm3 = 2.50m
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        return peso;
    }

    [Fact]
    public async Task Record_GlassWeightNotPositive_IsRefused_NoRowSaved()
    {
        // Deep sweep I3-M-3 (Diogo, 18/10/2026: o fisicamente impossível é
        // recusado): 1 g de água com punção 30 dá peso do vidro negativo.
        // Nenhuma linha do envio é gravada, nem a boa.
        var peso = await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            peso.Id, "12", 199.64m, [new PesoMeasurementRow("13", 1m)]));

        Assert.Equal(RecordPesoMeasurementResult.ResultNonPositive, outcome.Result);
        Assert.Equal(0, await _db.PesoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Record_AppendsHistoricalMeasurementWithBackendCalculation()
    {
        var peso = await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            peso.Id, "12", 199.64m));

        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
        Assert.NotNull(outcome.Measurement);

        // O número/posição CM visível é row-level measurement data; a massa
        // observada é registada na totalidade; a posição física é a ordem
        // de registo (1-based).
        Assert.Equal(peso.Id, outcome.Measurement!.PesoId);
        Assert.Equal(1, outcome.Measurement.RowPosition);
        Assert.Equal("12", outcome.Measurement.CmSubject);
        Assert.Equal(199.64m, outcome.Measurement.WaterWeightGrams);

        // Cálculo detido pelo backend com os inputs consumidos congelados
        // (densidade resolvida pela tabela real da fábrica à temperatura):
        // capacity = 199.64 / 0.99717 = 200.20658463451567937262452741;
        // peso do vidro = (200.20658463451567937262452741 + 10 - 30) × 2.50
        // = 450.51646158628919843156131852.
        Assert.Equal(200.20658463451567937262452741m, outcome.Measurement.CapacityCm3);
        Assert.Equal(450.51646158628919843156131852m, outcome.Measurement.GlassWeightGrams);

        var persisted = await _db.PesoMeasurements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Measurement.MeasurementId);
        Assert.Equal(peso.Id, persisted.PesoId);
        Assert.Equal(1, persisted.RowPosition);
        Assert.Equal("12", persisted.CmSubject);
        Assert.Equal(199.64m, persisted.WaterWeightGrams);
    }

    [Fact]
    public async Task Record_SequentialRowPositionsByRegistrationOrder()
    {
        var peso = await SeedPesoAsync();

        var first = await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "12", 199.64m));
        var second = await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "21", 99.82m));
        var third = await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "45", 199.64m));

        Assert.Equal(1, first.Measurement!.RowPosition);
        Assert.Equal(2, second.Measurement!.RowPosition);
        Assert.Equal(3, third.Measurement!.RowPosition);

        var measurements = await _db.PesoMeasurements.AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .OrderBy(m => m.RowPosition)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, measurements.Select(m => m.RowPosition).ToArray());
        Assert.Equal(new[] { "12", "21", "45" }, measurements.Select(m => m.CmSubject).ToArray());
    }

    [Fact]
    public async Task Record_SeveralLinesOfOneSubmit_SavedTogetherAfterTheExistingOnes()
    {
        // As linhas do «+» vão no mesmo comando e na mesma escrita, a seguir
        // às que já existem (deep sweep 18/10/2026).
        var peso = await SeedPesoAsync();
        await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "12", 199.64m));

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            peso.Id, "21", 99.82m, [new PesoMeasurementRow("45", 150.2m), new PesoMeasurementRow(null, 120m)]));

        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
        Assert.Equal(2, outcome.Measurement!.RowPosition);
        var measurements = await _db.PesoMeasurements.AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .OrderBy(m => m.RowPosition)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2, 3, 4 }, measurements.Select(m => m.RowPosition).ToArray());
        Assert.Equal(new[] { "12", "21", "45", "" }, measurements.Select(m => m.CmSubject).ToArray());
        Assert.Equal(new[] { 199.64m, 99.82m, 150.2m, 120m }, measurements.Select(m => m.WaterWeightGrams).ToArray());
    }

    [Fact]
    public async Task Record_AppendOnly_NeverRewritesExistingMeasurements()
    {
        // Leituras registadas são factos históricos: novos registos
        // acrescentam, nunca reescrevem nem apagam os anteriores.
        var peso = await SeedPesoAsync();

        var first = await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "12", 199.64m));
        await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "21", 99.82m));

        var persisted = await _db.PesoMeasurements.AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal(first.Measurement!.MeasurementId, persisted[0].Id);
        Assert.Equal(199.64m, persisted[0].WaterWeightGrams);
        Assert.Equal("12", persisted[0].CmSubject);
        Assert.Equal(99.82m, persisted[1].WaterWeightGrams);
    }

    [Fact]
    public async Task Record_DoesNotModifyPesoOrTools()
    {
        // O registo de leituras não altera o Peso, os inputs consumidos
        // congelados, as Tools nem o contexto técnico delas.
        var peso = await SeedPesoAsync();
        var pesoId = peso.Id;

        await _handler.HandleAsync(new RecordPesoMeasurement(pesoId, "12", 199.64m));
        await _handler.HandleAsync(new RecordPesoMeasurement(pesoId, "21", 99.82m));

        var pesoAfter = await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == pesoId);
        Assert.Equal("T3", pesoAfter.CmToolId);
        Assert.Equal("BQ-TOOL-9", pesoAfter.BqToolId);
        Assert.Equal("NNPB", pesoAfter.Process);
        Assert.Equal(20, pesoAfter.WaterTemperatureC);
        Assert.Equal(10m, pesoAfter.VolumeMarisaCm3);
        Assert.Equal(30m, pesoAfter.VolumePuncaoCm3);
        Assert.Equal(200m, pesoAfter.NominalWeightGrams);
        Assert.Equal(2.50m, pesoAfter.GlassDensityGramsPerCm3);

        Assert.Equal(2, await _db.ToolTechnicalContexts.AsNoTracking().CountAsync());
        var cmTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .SingleAsync(t => t.ToolId == "T3");
        Assert.Equal(30m, cmTechnical.VolumePuncaoCm3);
    }

    [Fact]
    public async Task Record_ObservedQuantityRegisteredInFull_NoSignConstraints()
    {
        // Nenhuma regra aritmética é imposta à persistência: a massa
        // observada é preservada na totalidade, tal como o registo de
        // movimentos de Boquilhas preserva a quantidade observada.
        var peso = await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            peso.Id, "54", 150.00m));

        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
        Assert.Equal(150.00m, outcome.Measurement!.WaterWeightGrams);

        var persisted = await _db.PesoMeasurements.AsNoTracking()
            .SingleAsync(m => m.PesoId == peso.Id);
        Assert.Equal(150.00m, persisted.WaterWeightGrams);
    }

    [Fact]
    public async Task Record_NullCmSubject_PersistedAsEmptyRowData()
    {
        // O número/posição CM visível é opcional como string: sem ele, a
        // posição física da linha continua a identificar a leitura.
        var peso = await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            peso.Id, null, 199.64m));

        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
        Assert.Equal(string.Empty, outcome.Measurement!.CmSubject);

        var persisted = await _db.PesoMeasurements.AsNoTracking()
            .SingleAsync(m => m.PesoId == peso.Id);
        Assert.Equal(string.Empty, persisted.CmSubject);
        Assert.Equal(1, persisted.RowPosition);
    }

    [Fact]
    public async Task Record_UnknownPeso_Refused()
    {
        await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            9999, "12", 199.64m));

        Assert.Equal(RecordPesoMeasurementResult.PesoNotFound, outcome.Result);
        Assert.Null(outcome.Measurement);
        Assert.Equal(0, await _db.PesoMeasurements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Record_CalculationUsesPesoInputsEvenIfToolContextChanges()
    {
        // Os inputs consumidos estão congelados no Peso: o cálculo da
        // leitura usa-os mesmo que o contexto técnico corrente da Tool ou a
        // configuração mudem depois da criação do Peso.
        var peso = await SeedPesoAsync();
        var pesoId = peso.Id;

        // A Tool BQ corrija o volume_marisa para 99 e a configuração muda
        // a densidade para 3.00 DEPOIS de o Peso existir.
        var bqTechnical = await _db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "BQ-TOOL-9");
        bqTechnical.VolumeMarisaCm3 = 99m;
        var glassDensity = await _db.GlassDensities.SingleAsync(g => g.Process == CmProcess.Nnpb);
        glassDensity.DensityGramsPerCm3 = 3.00m;
        await _db.SaveChangesAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(
            pesoId, "12", 199.64m));

        // O cálculo continua com os valores congelados no Peso (10 / 2.50),
        // não com o estado corrente mutado (capacity =
        // 199.64 / 0.99717 = 200.20658463451567937262452741; vidro =
        // 450.51646158628919843156131852).
        Assert.Equal(200.20658463451567937262452741m, outcome.Measurement!.CapacityCm3);
        Assert.Equal(450.51646158628919843156131852m, outcome.Measurement.GlassWeightGrams);

        var pesoAfter = await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == pesoId);
        Assert.Equal(10m, pesoAfter.VolumeMarisaCm3);
        Assert.Equal(2.50m, pesoAfter.GlassDensityGramsPerCm3);
    }

    /// <summary>
    /// A produção com o Peso semeado (CM T3, BQ-TOOL-9) e, depois de trocar a
    /// CM para T4, o Peso das ferramentas novas.
    /// </summary>
    private async Task<(Peso Antigo, Peso Atual)> SeedSwappedProductionAsync()
    {
        var antigo = await SeedPesoAsync();
        _db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = "202601", Machine = new Machine { Code = "B1" } };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        _db.CmContexts.Add(new CmContext { ToolId = "T3", ReplacedJobOnId = jobOn.Id, ReplacedAtUtc = DateTime.UtcNow });
        _db.CmContexts.Add(new CmContext { ToolId = "T4", JobOnId = jobOn.Id });
        _db.BqContexts.Add(new BqContext { ToolId = "BQ-TOOL-9", JobOnId = jobOn.Id });
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        antigo.ControloId = controlo.Id;
        var atual = new Peso
        {
            CmToolId = "T4",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id
        };
        _db.Pesos.Add(atual);
        await _db.SaveChangesAsync();
        return (antigo, atual);
    }

    // Depois de trocar a CM, o Peso anterior já não recebe leituras novas: o
    // cálculo seria o das ferramentas antigas (respostas R15 e R16-3 do
    // especialista, 18/10/2026). O Peso das ferramentas atuais grava.
    [Fact]
    public async Task PesoIdAntigo_Recusa_PesoNotCurrent()
    {
        var (antigo, atual) = await SeedSwappedProductionAsync();

        var recusada = await _handler.HandleAsync(new RecordPesoMeasurement(antigo.Id, "12", 199.64m));
        var gravada = await _handler.HandleAsync(new RecordPesoMeasurement(atual.Id, "12", 199.64m));

        Assert.Equal(RecordPesoMeasurementResult.PesoNotCurrent, recusada.Result);
        Assert.Null(recusada.Measurement);
        Assert.Equal(RecordPesoMeasurementResult.Success, gravada.Result);
        var leituras = await _db.PesoMeasurements.AsNoTracking().ToListAsync();
        Assert.Equal(atual.Id, Assert.Single(leituras).PesoId);
    }

    // Um Peso sem produção não tem ferramentas atuais a comparar: continua a
    // gravar.
    [Fact]
    public async Task PesoSemProducao_ContinuaAGravar()
    {
        var peso = await SeedPesoAsync();

        var outcome = await _handler.HandleAsync(new RecordPesoMeasurement(peso.Id, "12", 199.64m));

        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync()).ControloId);
    }
}

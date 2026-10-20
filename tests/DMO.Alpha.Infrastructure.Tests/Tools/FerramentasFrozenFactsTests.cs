using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Prova o CONGELAMENTO DOS FATOS HUMANOS à criação pelos handlers de
/// consumo (item 12 do contrato aprovado — o histórico mostra
/// «CM · 5447 · lote 3» mesmo depois de a Tool ser editada ou eliminada
/// com reatribuição à DUMMY) e a PORTA FECHADA da linha DUMMY em todos os
/// caminhos de consumo: a tombstone REMOVED nunca é utilizável como Tool
/// real (recusa tipada ToolNotFound — os pickers excluem-na e o backend
/// fecha a porta), nos traces de reparação, nos Pesos tool-scoped, nos
/// registos de Pegamentos (herdados do contexto Job On) e no próprio Job
/// On.
/// </summary>
public sealed class FerramentasFrozenFactsTests : IDisposable
{
    private readonly DmoDbContext _db;

    public FerramentasFrozenFactsTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task SeedDummyAndToolsAsync()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" },
            new Tool { ToolId = "MF-X", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBqRepairTrace_FreezesToolFactsAtCreation()
    {
        await SeedDummyAndToolsAsync();

        var handler = new CreateBqRepairTraceHandler(_db);
        var outcome = await handler.HandleAsync(new CreateBqRepairTrace("BQ-X"));
        Assert.Equal(CreateBqRepairTraceResult.Success, outcome.Result);

        // Os FATOS HUMANOS da tool ficam congelados no trace à criação
        // (evidência histórica: sobrevivem à edição/eliminação da Tool).
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Equal("T194", trace.ToolReferenceFrozen);
        Assert.Equal("12", trace.ToolLotFrozen);
    }

    [Fact]
    public async Task CreateBqRepairTrace_DummyToolIsRefusedTyped()
    {
        await SeedDummyAndToolsAsync();

        var handler = new CreateBqRepairTraceHandler(_db);
        var outcome = await handler.HandleAsync(new CreateBqRepairTrace(ToolRegistryConstants.RemovedToolId));

        Assert.Equal(CreateBqRepairTraceResult.ToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.BqRepairTraces.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateToolScopedPeso_FreezesToolFactsAtCreation()
    {
        await SeedDummyAndToolsAsync();
        _db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = "CM-X", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 1.4m },
            new ToolTechnicalContext { ToolId = "BQ-X", VolumeMarisaCm3 = 2.5m });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.5m });
        await _db.SaveChangesAsync();

        var handler = new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db));
        var outcome = await handler.HandleAsync(new CreateToolScopedPeso("CM-X", "BQ-X", 20.4m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);

        // O Peso congela os factos humanos de AMBAS as tools usadas: o
        // histórico da Tool mostra-os mesmo depois de editada/eliminada.
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal("5447", peso.CmToolReferenceFrozen);
        Assert.Equal("3", peso.CmToolLotFrozen);
        Assert.Equal("T194", peso.BqToolReferenceFrozen);
        Assert.Equal("12", peso.BqToolLotFrozen);
    }

    [Fact]
    public async Task CreateToolScopedPeso_DummyToolIsRefusedTyped()
    {
        await SeedDummyAndToolsAsync();
        _db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = "CM-X", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 1.4m },
            new ToolTechnicalContext { ToolId = "BQ-X", VolumeMarisaCm3 = 2.5m });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.5m });
        await _db.SaveChangesAsync();

        var handler = new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db));

        // A tombstone nunca é uma Tool real de pesagem — CM ou BQ.
        var cmDummy = await handler.HandleAsync(new CreateToolScopedPeso(ToolRegistryConstants.RemovedToolId, "BQ-X", 20m));
        Assert.Equal(CreateToolScopedPesoResult.CmToolNotFound, cmDummy.Result);

        var bqDummy = await handler.HandleAsync(new CreateToolScopedPeso("CM-X", ToolRegistryConstants.RemovedToolId, 20m));
        Assert.Equal(CreateToolScopedPesoResult.BqToolNotFound, bqDummy.Result);

        Assert.Equal(0, await _db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RecordPegamentosMeasurement_FreezesCombinationFactsAtRegistroCreation()
    {
        // A exceção cirúrgica aprovada: o handler de consumo congela as
        // âncoras humanas (referência/lote por componente) no REGISTO de
        // Pegamentos à criação — as medições continuam intocadas.
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" },
            new Tool { ToolId = "MF-X", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "CM-X", DiametroGargaloMm = 70.00m });
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var jobOnHandler = new CreateJobOnHandler(_db);
        var created = await jobOnHandler.HandleAsync(new CreateJobOn(
            "PN-1", "5810", machine.Id, "CM-X", "MF-X", "BQ-X"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        var handler = new RecordPegamentosMeasurementHandler(_db, new PegamentosToleranceConfiguration(_db));
        var outcome = await handler.HandleAsync(new RecordPegamentosMeasurement(controlo.Id, "CM", 70.10m));

        Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);

        // O registo durável congela os factos humanos da combinação
        // CM+BQ+MF herdada do contexto Job On.
        var registro = await _db.Pegamentos.AsNoTracking().SingleAsync();
        Assert.Equal("5447", registro.CmToolReferenceFrozen);
        Assert.Equal("3", registro.CmToolLotFrozen);
        Assert.Equal("T194", registro.BqToolReferenceFrozen);
        Assert.Equal("12", registro.BqToolLotFrozen);
        Assert.Equal("5810", registro.MfToolReferenceFrozen);
        Assert.Equal("1", registro.MfToolLotFrozen);
    }

    [Fact]
    public async Task RecordPegamentosMeasurement_DummyComponentIsRefusedAsNotInProduction()
    {
        // Produção LEGADA sem MF (criada antes da regra do Owner
        // 2026-10-10 «jobon necessita sempre de MF» — o handler recusa
        // esse estado à criação, mas o dado antigo persiste na base):
        // medir MF é recusa ComponentNotInProduction — a tombstone nunca
        // entra pela folha (a herança vem do contexto Job On, nunca da
        // seleção local).
        _db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "CM-X", DiametroGargaloMm = 70.00m });
        await _db.SaveChangesAsync();

        // O Job On legado semeado diretamente (contextos CM/BQ, nenhum
        // mf_id — estado pré-regra) com o seu controlo.
        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = "PN-1", MachineId = machine.Id };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-X" });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-X" });
        _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn });
        await _db.SaveChangesAsync();

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        var handler = new RecordPegamentosMeasurementHandler(_db, new PegamentosToleranceConfiguration(_db));
        var outcome = await handler.HandleAsync(new RecordPegamentosMeasurement(controlo.Id, "MF", 59.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.ComponentNotInProduction, outcome.Result);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RecordPegamentosMeasurement_DeletedMfInTheProduction_IsRefusedAsNotInProduction()
    {
        // Desde a AddToolFksToProduction, apagar a MF deixa a produção a
        // apontar para a DUMMY. Medir essa MF é a mesma recusa de uma
        // produção sem MF: a ferramenta já não existe.
        _db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "CM-X", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "BQ-X", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.Machines.Add(new Machine { Code = "B1" });
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = "PN-1", MachineId = machine.Id };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-X" });
        _db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = ToolRegistryConstants.RemovedToolId });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-X" });
        _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn });
        await _db.SaveChangesAsync();

        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        var handler = new RecordPegamentosMeasurementHandler(_db, new PegamentosToleranceConfiguration(_db));
        var outcome = await handler.HandleAsync(new RecordPegamentosMeasurement(controlo.Id, "MF", 59.00m, null, 59.00m));

        Assert.Equal(RecordPegamentosMeasurementResult.ComponentNotInProduction, outcome.Result);
        Assert.Equal(0, await _db.Pegamentos.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.ToolTechnicalContexts.AsNoTracking()
            .CountAsync(t => t.ToolId == ToolRegistryConstants.RemovedToolId));
    }

    [Fact]
    public async Task CreateJobOn_DummyToolIsRefusedTypedInEverySlot()
    {
        await SeedDummyAndToolsAsync();
        _db.Machines.Add(new Machine { Code = "B1" });
        await _db.SaveChangesAsync();

        var machine = await _db.Machines.AsNoTracking().SingleAsync();
        var handler = new CreateJobOnHandler(_db);

        // A tombstone não é selecionável em NENHUM slot: os pickers
        // excluem-na e o backend fecha a porta com recusa tipada. O MF é
        // obrigatório em todo o Job On (regra do Owner 2026-10-10), pelo
        // que os casos CM/BQ levam o MF real selecionado — a recusa é
        // sempre a do slot em causa.
        var cm = await handler.HandleAsync(new CreateJobOn(
            "PN-1", "5810", machine.Id, ToolRegistryConstants.RemovedToolId, "MF-X", null));
        Assert.Equal(CreateJobOnResult.CmToolNotFound, cm.Result);

        var mf = await handler.HandleAsync(new CreateJobOn("PN-2", "5810", machine.Id, null, ToolRegistryConstants.RemovedToolId, null));
        Assert.Equal(CreateJobOnResult.MfToolNotFound, mf.Result);

        var bq = await handler.HandleAsync(new CreateJobOn(
            "PN-3", "5810", machine.Id, null, "MF-X", ToolRegistryConstants.RemovedToolId));
        Assert.Equal(CreateJobOnResult.BqToolNotFound, bq.Result);

        // Nenhum Job On nem contexto foi criado.
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());
    }
}

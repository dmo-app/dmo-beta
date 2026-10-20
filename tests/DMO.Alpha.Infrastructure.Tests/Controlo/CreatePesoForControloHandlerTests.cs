using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

public sealed class CreatePesoForControloHandlerTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmoDbContext NewContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(interceptors)
            .Options);

    private async Task<int> SeedAsync()
    {
        await using var db = NewContext();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        db.Tools.Add(new Tool { ToolId = "BQ-10", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
        db.Tools.Add(new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "M10", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "MF-2", Type = ToolType.Mf, Reference = "M10", Lot = "2" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-9", VolumeMarisaCm3 = 10m });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T4", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 32m });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-10", VolumeMarisaCm3 = 11m });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        var jobOn = new JobOn { Reference = "1075C142", ProductionNumber = "202601", Machine = machine };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-9" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-1" });
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        return controlo.Id;
    }

    private static CreatePesoForControloHandler Handler(DmoDbContext db) =>
        new(db, new CreateToolScopedPesoHandler(db, new GlassDensityConfiguration(db)));

    /// <summary>
    /// A troca da CM ou da BQ a meio da produção, como o UpdateJobOnHandler:
    /// o contexto anterior fica ligado por ReplacedJobOnId; voltar a uma
    /// ferramenta já usada retoma o contexto dela.
    /// </summary>
    private async Task SwapAsync(int controloId, string? cmToolId = null, string? bqToolId = null, string? mfToolId = null)
    {
        await using var db = NewContext();
        var jobOnId = (await db.Controlos.SingleAsync(c => c.Id == controloId)).JobOnId;
        if (cmToolId is not null)
        {
            var current = await db.CmContexts.SingleAsync(c => c.JobOnId == jobOnId);
            current.JobOnId = null;
            current.ReplacedJobOnId = jobOnId;
            var previous = await db.CmContexts.FirstOrDefaultAsync(c => c.ReplacedJobOnId == jobOnId && c.ToolId == cmToolId);
            if (previous is not null)
            {
                previous.ReplacedJobOnId = null;
                previous.JobOnId = jobOnId;
            }
            else
            {
                db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = cmToolId });
            }
        }

        if (bqToolId is not null)
        {
            var current = await db.BqContexts.SingleAsync(c => c.JobOnId == jobOnId);
            current.JobOnId = null;
            current.ReplacedJobOnId = jobOnId;
            db.BqContexts.Add(new BqContext { JobOnId = jobOnId, ToolId = bqToolId });
        }

        if (mfToolId is not null)
        {
            (await db.MfContexts.SingleAsync(c => c.JobOnId == jobOnId)).ToolId = mfToolId;
        }

        await db.SaveChangesAsync();
    }

    private async Task AddReadingAsync(int pesoId, decimal waterWeightGrams)
    {
        await using var db = NewContext();
        var outcome = await new RecordPesoMeasurementHandler(db)
            .HandleAsync(new RecordPesoMeasurement(pesoId, "1", waterWeightGrams));
        Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
    }

    // A troca da CM a meio da produção abre um Peso novo com as ferramentas
    // novas; o anterior fica como estava (respostas R15 e R16-3 do
    // especialista, 18/10/2026).
    [Fact]
    public async Task TrocaDeCm_CriaSegundoPeso_OAntigoMantemAsLeituras()
    {
        var controloId = await SeedAsync();
        int primeiro;
        await using (var db = NewContext())
        {
            primeiro = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
        }

        await AddReadingAsync(primeiro, 200m);
        await SwapAsync(controloId, cmToolId: "T4");

        await using var after = NewContext();
        var outcome = await Handler(after).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.Equal(CreatePesoForControloResult.Success, outcome.Result);
        Assert.NotEqual(primeiro, outcome.Peso!.PesoId);
        Assert.Equal("T4", outcome.Peso.CmToolId);
        Assert.Equal(32m, outcome.Peso.VolumePuncaoCm3);
        var pesos = await after.Pesos.AsNoTracking().Include(p => p.Measurements).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, pesos.Count);
        Assert.All(pesos, p => Assert.Equal(controloId, p.ControloId));
        Assert.Equal("T3", pesos[0].CmToolId);
        Assert.Equal(30m, pesos[0].VolumePuncaoCm3);
        Assert.Single(pesos[0].Measurements);
        Assert.Empty(pesos[1].Measurements);
    }

    // A BQ entra no cálculo (o volume marisa): trocá-la também abre um Peso
    // novo (resposta R16-5 do especialista).
    [Fact]
    public async Task TrocaDeBq_CriaPesoNovo()
    {
        var controloId = await SeedAsync();
        await using var db = NewContext();
        var primeiro = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
        await SwapAsync(controloId, bqToolId: "BQ-10");

        await using var after = NewContext();
        var outcome = await Handler(after).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.NotEqual(primeiro, outcome.Peso!.PesoId);
        Assert.Equal("BQ-10", outcome.Peso.BqToolId);
        Assert.Equal(11m, outcome.Peso.VolumeMarisaCm3);
        Assert.Equal(2, await after.Pesos.CountAsync(p => p.ControloId == controloId));
    }

    // O MF não entra no cálculo do Peso: trocá-lo não abre outro Peso
    // (resposta R16-5 do especialista).
    [Fact]
    public async Task TrocaSoDoMf_MantemOPeso()
    {
        var controloId = await SeedAsync();
        await using var db = NewContext();
        var primeiro = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
        await SwapAsync(controloId, mfToolId: "MF-2");

        await using var after = NewContext();
        var outcome = await Handler(after).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.Equal(primeiro, outcome.Peso!.PesoId);
        Assert.Single(await after.Pesos.ToListAsync());
    }

    // A→B→A: voltar à CM anterior retoma o Peso dela, como o contexto
    // (resposta R16-2 do especialista).
    [Fact]
    public async Task VoltarACmAnterior_ReabreOPesoA()
    {
        var controloId = await SeedAsync();
        int pesoA;
        int pesoB;
        await using (var db = NewContext())
        {
            pesoA = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
        }

        await SwapAsync(controloId, cmToolId: "T4");
        await using (var db = NewContext())
        {
            pesoB = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
        }

        await SwapAsync(controloId, cmToolId: "T3");
        await using var after = NewContext();
        var outcome = await Handler(after).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.NotEqual(pesoA, pesoB);
        Assert.Equal(pesoA, outcome.Peso!.PesoId);
        Assert.Equal(2, await after.Pesos.CountAsync());
        await AddReadingAsync(pesoA, 200m);
        await using var check = NewContext();
        var recusada = await new RecordPesoMeasurementHandler(check)
            .HandleAsync(new RecordPesoMeasurement(pesoB, "1", 200m));
        Assert.Equal(RecordPesoMeasurementResult.PesoNotCurrent, recusada.Result);
    }

    // O Peso novo copia a produção anterior do Peso que a produção já tem
    // (resposta R16-7 do especialista); continua editável.
    [Fact]
    public async Task PesoNovo_CopiaAProducaoAnteriorDoPesoDaProducao()
    {
        var controloId = await SeedAsync();
        int anteriorControloId;
        await using (var db = NewContext())
        {
            var outra = new DMO.Alpha.Core.Controlo.Controlo { JobOn = new JobOn { Reference = "1075C142", ProductionNumber = "202500", MachineId = (await db.Machines.SingleAsync()).Id } };
            db.Controlos.Add(outra);
            await db.SaveChangesAsync();
            anteriorControloId = outra.Id;
            var pesoId = (await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m))).Peso!.PesoId;
            var peso = await db.Pesos.SingleAsync(p => p.Id == pesoId);
            peso.ProducaoAnteriorControloId = anteriorControloId;
            peso.ProducaoAnteriorPesoGramas = 412.5m;
            await db.SaveChangesAsync();
        }

        await SwapAsync(controloId, cmToolId: "T4");
        await using var after = NewContext();
        var outcome = await Handler(after).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        var novo = await after.Pesos.AsNoTracking().SingleAsync(p => p.Id == outcome.Peso!.PesoId);
        Assert.Equal(anteriorControloId, novo.ProducaoAnteriorControloId);
        Assert.Equal(412.5m, novo.ProducaoAnteriorPesoGramas);
    }

    [Fact]
    public async Task Create_SavesThePesoAlreadyLinkedAndReturnsItAgain()
    {
        var controloId = await SeedAsync();
        await using var db = NewContext();

        var first = await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m));
        var second = await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.Equal(CreatePesoForControloResult.Success, first.Result);
        Assert.Equal(first.Peso!.PesoId, second.Peso!.PesoId);
        var peso = Assert.Single(await db.Pesos.ToListAsync());
        Assert.Equal(controloId, peso.ControloId);
    }

    [Fact]
    public async Task Create_WhenAnotherRequestSavedFirst_ReturnsThatPesoWithoutAnOrphan()
    {
        var controloId = await SeedAsync();
        await using var db = NewContext(new ConcurrentPesoInterceptor(NewContext));

        var outcome = await Handler(db).HandleAsync(new CreatePesoForControlo(controloId, 20m));

        Assert.Equal(CreatePesoForControloResult.Success, outcome.Result);
        await using var check = NewContext();
        var peso = Assert.Single(await check.Pesos.ToListAsync());
        Assert.Equal(peso.Id, outcome.Peso!.PesoId);
        Assert.Equal(controloId, peso.ControloId);
    }

    /// <summary>Simula o duplo clique: antes de gravar, outro pedido grava o Peso do mesmo controlo.</summary>
    private sealed class ConcurrentPesoInterceptor(Func<IInterceptor[], DmoDbContext> newContext) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var added = eventData.Context!.ChangeTracker.Entries<Peso>()
                .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.ControloId is not null);
            if (!_fired && added is not null)
            {
                _fired = true;
                await using var other = newContext([]);
                var winner = added.Entity;
                other.Pesos.Add(new Peso
                {
                    CmToolId = winner.CmToolId,
                    BqToolId = winner.BqToolId,
                    Process = winner.Process,
                    WaterTemperatureC = winner.WaterTemperatureC,
                    VolumeMarisaCm3 = winner.VolumeMarisaCm3,
                    VolumePuncaoCm3 = winner.VolumePuncaoCm3,
                    GlassDensityGramsPerCm3 = winner.GlassDensityGramsPerCm3,
                    ControloId = winner.ControloId
                });
                await other.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("duplicate key value violates unique constraint");
            }

            return result;
        }
    }
}

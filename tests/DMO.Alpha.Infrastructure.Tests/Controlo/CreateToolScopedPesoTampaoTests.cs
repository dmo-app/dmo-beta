using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O Peso congela o Tampão com a punção (Diogo, 23/10/2026): o do Job On do
/// controlo; sem Job On, o que a CM ainda tem de arranque; sem nenhum, fica
/// vazio e o Peso cria-se na mesma.
/// </summary>
public sealed class CreateToolScopedPesoTampaoTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateToolScopedPesoHandler _handler;

    public CreateToolScopedPesoTampaoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CreateToolScopedPesoHandler(_db, new GlassDensityConfiguration(_db));
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedFerramentasAsync(decimal? cmD, decimal? cmH)
    {
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m, NominalWeightGrams = 200m,
            TpDiametroMm = cmD, TpSagittaMm = cmH
        });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ9", VolumeMarisaCm3 = 10m });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        await _db.SaveChangesAsync();
    }

    private async Task<int> SeedControloAsync(decimal? d, decimal? calote)
    {
        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "1075C142", ProductionNumber = "PN-TAMPAO-1", Machine = machine,
            TampaoDiametroMm = d, CaloteMm = calote
        };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        return controlo.Id;
    }

    [Fact]
    public async Task ComJobOn_CongelaOTampaoDoJobOn()
    {
        await SeedFerramentasAsync(cmD: 50m, cmH: 10m);
        var controloId = await SeedControloAsync(d: 40m, calote: 9m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ9", 20m, ControloId: controloId));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(40m, peso.TampaoDiametroMm);
        Assert.Equal(9m, peso.TampaoCaloteMm);
    }

    [Fact]
    public async Task SemJobOn_CongelaOQueACmTemDeArranque()
    {
        await SeedFerramentasAsync(cmD: 50m, cmH: 10m);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(50m, peso.TampaoDiametroMm);
        Assert.Equal(10m, peso.TampaoCaloteMm);
    }

    [Fact]
    public async Task SemTampaoEmLadoNenhum_PesoCriaSeComOTampaoVazio()
    {
        await SeedFerramentasAsync(cmD: null, cmH: null);

        var outcome = await _handler.HandleAsync(new CreateToolScopedPeso("T3", "BQ9", 20m));

        Assert.Equal(CreateToolScopedPesoResult.Success, outcome.Result);
        var peso = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Null(peso.TampaoDiametroMm);
        Assert.Null(peso.TampaoCaloteMm);
    }
}

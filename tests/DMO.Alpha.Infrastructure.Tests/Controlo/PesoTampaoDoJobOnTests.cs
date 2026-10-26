using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O Tampão é da produção (Diogo, 23/10/2026): o D e a calote do Job On
/// mandam no apoio do Peso; o que a CM ainda tem fica só de arranque.
/// </summary>
public sealed class PesoTampaoDoJobOnTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoCapSupportQuery _query;

    public PesoTampaoDoJobOnTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoCapSupportQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedAsync(decimal? toolD, decimal? toolH, decimal? jobOnD, decimal? jobOnCalote)
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            TpDiametroMm = toolD,
            TpSagittaMm = toolH
        });
        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = $"PN-{Guid.NewGuid():N}"[..12],
            Machine = machine,
            CaloteMm = jobOnCalote,
            TampaoDiametroMm = jobOnD
        };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        return controlo.Id;
    }

    [Fact]
    public async Task DiametroDoJobOn_MandaSobreODaCm()
    {
        var controloId = await SeedAsync(toolD: 50m, toolH: 10m, jobOnD: 40m, jobOnCalote: 9m);

        var apoio = await _query.GetAsync(new PesoCapSupportRequest("T3", controloId));

        Assert.True(apoio.TemReferencia);
        Assert.Equal(40m, apoio.DiametroMm);
        Assert.Equal("Job On", apoio.OrigemCalote);
    }

    [Fact]
    public async Task SemTampaoNaCm_OJobOnBasta()
    {
        // A CM já não precisa do Tampão: D e calote vêm da produção.
        var controloId = await SeedAsync(toolD: null, toolH: null, jobOnD: 40m, jobOnCalote: 9m);

        var apoio = await _query.GetAsync(new PesoCapSupportRequest("T3", controloId));

        Assert.True(apoio.TemReferencia);
        Assert.Equal(40m, apoio.DiametroMm);
        Assert.Equal(9m, apoio.AlturaEfetivaMm);
    }

    [Fact]
    public async Task JobOnSemDiametro_CaiNoDaCmDeArranque()
    {
        var controloId = await SeedAsync(toolD: 50m, toolH: 10m, jobOnD: null, jobOnCalote: null);

        var apoio = await _query.GetAsync(new PesoCapSupportRequest("T3", controloId));

        Assert.True(apoio.TemReferencia);
        Assert.Equal(50m, apoio.DiametroMm);
        Assert.Equal("Tool CM", apoio.OrigemCalote);
    }

    [Fact]
    public async Task SemTampaoEmLado_Nenhum_ReferenciaPorCompletarSemBloquear()
    {
        var controloId = await SeedAsync(toolD: null, toolH: null, jobOnD: null, jobOnCalote: null);

        var apoio = await _query.GetAsync(new PesoCapSupportRequest("T3", controloId));

        Assert.False(apoio.TemReferencia);
        Assert.Contains("Folha do Job On", apoio.EstadoMensagem);
    }

    [Fact]
    public void FromKnown_UsaODiametroDoJobOn()
    {
        var apoio = PesoCapSupportQuery.FromKnown(null, caloteJobOn: 9m, caloteSimuladaMm: null, diametroJobOn: 40m);

        Assert.True(apoio.TemReferencia);
        Assert.Equal(40m, apoio.DiametroMm);
    }
}

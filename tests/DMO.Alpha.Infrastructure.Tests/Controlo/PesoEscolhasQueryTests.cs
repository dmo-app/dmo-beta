using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// The four lookups the Peso page used to run against the DbContext itself:
/// same answers as before, and nothing is written.
/// </summary>
public sealed class PesoEscolhasQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ControloDaProducao_DevolveOControloDessaProducaoESemControloDevolveNulo()
    {
        var (com, sem) = await SeedProducoesAsync();
        var query = new PesoEscolhasQuery(_db);

        var controloId = await _db.Controlos.Where(c => c.JobOnId == com).Select(c => c.Id).SingleAsync();
        Assert.Equal(controloId, await query.ControloDaProducaoAsync(com));
        Assert.Null(await query.ControloDaProducaoAsync(sem));
        Assert.Null(await query.ControloDaProducaoAsync(-1));
    }

    [Fact]
    public async Task ProducaoDoControlo_DevolveAProducaoEControloInexistenteDevolveNulo()
    {
        var (com, _) = await SeedProducoesAsync();
        var query = new PesoEscolhasQuery(_db);

        var controloId = await _db.Controlos.Where(c => c.JobOnId == com).Select(c => c.Id).SingleAsync();
        Assert.Equal(com, await query.ProducaoDoControloAsync(controloId));
        Assert.Null(await query.ProducaoDoControloAsync(controloId + 100));
    }

    [Fact]
    public async Task CodigosDeMaquinas_VemPorOrdemDoCodigo()
    {
        _db.Machines.AddRange(new Machine { Code = "C3" }, new Machine { Code = "B1" }, new Machine { Code = "A2" });
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { "A2", "B1", "C3" }, await new PesoEscolhasQuery(_db).CodigosDeMaquinasAsync());
    }

    [Fact]
    public async Task ComparacaoGuardada_SoExisteComOParDeIdentificadoresCerto()
    {
        var (com, _) = await SeedProducoesAsync();
        var controloId = await _db.Controlos.Where(c => c.JobOnId == com).Select(c => c.Id).SingleAsync();
        _db.Tools.AddRange(
            new Tool { ToolId = "CM", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" });
        var peso = new Peso
        {
            ControloId = controloId, CmToolId = "CM", BqToolId = "BQ", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.5m
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        var comparacao = new PesoComparacao
        {
            ControloId = controloId, PesoInicialId = peso.Id, CmToolId = "CM",
            WaterTemperatureC = 20, WaterDensityGramsPerCm3 = 0.99717m, GlassDensityGramsPerCm3 = 2.5m,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, CreatedAtUtc = new DateTime(2026, 10, 17, 7, 0, 0, DateTimeKind.Utc)
        };
        _db.PesoComparacoes.Add(comparacao);
        await _db.SaveChangesAsync();
        var query = new PesoEscolhasQuery(_db);

        Assert.True(await query.ComparacaoGuardadaExisteAsync(comparacao.Id, peso.Id));
        Assert.False(await query.ComparacaoGuardadaExisteAsync(comparacao.Id, peso.Id + 1));
        Assert.False(await query.ComparacaoGuardadaExisteAsync(comparacao.Id + 1, peso.Id));
    }

    [Fact]
    public async Task OsQuatroPedidos_NaoGravamNada()
    {
        var (com, _) = await SeedProducoesAsync();
        var query = new PesoEscolhasQuery(_db);
        _db.ChangeTracker.Clear();

        await query.ControloDaProducaoAsync(com);
        await query.ProducaoDoControloAsync(1);
        await query.CodigosDeMaquinasAsync();
        await query.ComparacaoGuardadaExisteAsync(1, 1);

        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private async Task<(int ComControlo, int SemControlo)> SeedProducoesAsync()
    {
        var machine = new Machine { Code = "B1" };
        var com = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "202605" };
        var sem = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "202606" };
        _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = com });
        _db.JobOns.Add(sem);
        await _db.SaveChangesAsync();
        return (com.Id, sem.Id);
    }
}

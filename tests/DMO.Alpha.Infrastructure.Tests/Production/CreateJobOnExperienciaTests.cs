using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// A experiência no Criar Job On (Diogo, 26/10/2026): número AAAA00, entra e
/// sai no mesmo dia, fica marcada e convive na linha com a produção em curso.
/// </summary>
public sealed class CreateJobOnExperienciaTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _handler;

    public CreateJobOnExperienciaTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CreateJobOnHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Machine Machine, Tool Mf)> SeedAsync()
    {
        var machine = new Machine { Code = "B3" };
        var mf = new Tool { ToolId = "MF-EXP", Type = ToolType.Mf, Reference = "9389", Lot = "1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(mf);
        await _db.SaveChangesAsync();
        return (machine, mf);
    }

    [Fact]
    public async Task Experiencia_UsaAnoZeroZero_EntraESaiNoMesmoDia_ENaoBloqueiaAProducaoDaLinha()
    {
        var (machine, mf) = await SeedAsync();
        var dia = new DateOnly(2026, 10, 26);

        var producao = await _handler.HandleAsync(new CreateJobOn(
            "202606", "9389T194", machine.Id, MfToolId: mf.ToolId, InicioEm: new DateOnly(2026, 10, 21)));
        var experiencia = await _handler.HandleAsync(new CreateJobOn(
            "qualquer", "9389T194", machine.Id, MfToolId: mf.ToolId, InicioEm: dia, FimEm: new DateOnly(2026, 11, 30), Experiencia: true));

        Assert.Equal(CreateJobOnResult.Success, producao.Result);
        Assert.Equal(CreateJobOnResult.Success, experiencia.Result);
        Assert.Null(experiencia.OverlapWarning);
        var gravada = await _db.JobOns.SingleAsync(j => j.Id == experiencia.JobOn!.JobOnId);
        Assert.True(gravada.Experiencia);
        Assert.Equal("202600", gravada.ProductionNumber);
        Assert.Equal(dia, gravada.InicioEm);
        Assert.Equal(dia, gravada.FimEm);
        Assert.False((await _db.JobOns.SingleAsync(j => j.Id == producao.JobOn!.JobOnId)).Experiencia);
    }
}

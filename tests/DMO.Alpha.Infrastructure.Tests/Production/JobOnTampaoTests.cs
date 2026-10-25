using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O diâmetro do tampão da produção (Diogo, 23/10/2026): grava na criação, só
/// muda na edição quando o envio o traz, zero ou negativo é recusa tipada e a
/// mudança fica registada para o Peso rever.
/// </summary>
public sealed class JobOnTampaoTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _create;
    private readonly UpdateJobOnHandler _update;

    public JobOnTampaoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _create = new CreateJobOnHandler(_db);
        _update = new UpdateJobOnHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Machine Machine, int JobOnId)> SeedAsync(decimal? tampao)
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();
        var outcome = await _create.HandleAsync(new CreateJobOn(
            $"PN-{Guid.NewGuid():N}"[..12], "1075C142", machine.Id, null, "T-MF",
            TampaoDiametroMm: tampao));
        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        return (machine, outcome.JobOn!.JobOnId);
    }

    [Fact]
    public async Task Create_GravaODiametro()
    {
        var (_, id) = await SeedAsync(40.5m);

        Assert.Equal(40.5m, (await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == id)).TampaoDiametroMm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_ZeroOuNegativo_RecusaSemEscrita(int valor)
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();

        var outcome = await _create.HandleAsync(new CreateJobOn(
            "PN-TAMPAO-0", "1075C142", machine.Id, null, "T-MF", TampaoDiametroMm: valor));

        Assert.Equal(CreateJobOnResult.InvalidTampao, outcome.Result);
        Assert.False(await _db.JobOns.AnyAsync(j => j.ProductionNumber == "PN-TAMPAO-0"));
    }

    [Fact]
    public async Task Update_SemOCampo_NaoLimpaOTampao()
    {
        var (machine, id) = await SeedAsync(40m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(id, machine.Id, null, "T-MF"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.Equal(40m, (await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == id)).TampaoDiametroMm);
    }

    [Fact]
    public async Task Update_ComOCampo_GravaERegistaAMudanca()
    {
        var (machine, id) = await SeedAsync(40m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            id, machine.Id, null, "T-MF", TampaoDiametroMm: 42m, TampaoEnviado: true));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.Equal(42m, (await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == id)).TampaoDiametroMm);
        Assert.Contains(await _db.JobOnChanges.AsNoTracking().ToListAsync(), c => c.Field == JobOnChangeFields.Tampao);
    }

    [Fact]
    public async Task Update_EnviadoVazio_LimpaOTampao()
    {
        var (machine, id) = await SeedAsync(40m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            id, machine.Id, null, "T-MF", TampaoDiametroMm: null, TampaoEnviado: true));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.Null((await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == id)).TampaoDiametroMm);
    }

    [Fact]
    public async Task Update_Zero_RecusaSemMexerNoFacto()
    {
        var (machine, id) = await SeedAsync(40m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            id, machine.Id, null, "T-MF", TampaoDiametroMm: 0m, TampaoEnviado: true));

        Assert.Equal(UpdateJobOnResult.InvalidTampao, outcome.Result);
        Assert.Equal(40m, (await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == id)).TampaoDiametroMm);
    }
}

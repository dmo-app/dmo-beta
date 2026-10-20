using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova a CALOTE DESTA PRODUÇÃO no write path do Job On (o campo
/// j-calote da ficha do dmo-html, a migração aditiva da B1): a calote
/// submetida na criação grava no Job On; a edição grava-a SEMPRE com o
/// valor submetido (o campo vem preenchido com o valor corrente da
/// ficha); limpar (null) volta à referência da Tool; negativa ou ilegível
/// é recusa tipada (InvalidCalote) antes de qualquer escrita. A calote
/// alimenta o apoio TP/calote do Peso (a B1) — nunca um gate.
/// </summary>
public sealed class JobOnCaloteTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _create;
    private readonly UpdateJobOnHandler _update;

    public JobOnCaloteTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _create = new CreateJobOnHandler(_db);
        _update = new UpdateJobOnHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(Machine Machine, int JobOnId)> SeedProducaoAsync(decimal? caloteMm = null)
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();

        var outcome = await _create.HandleAsync(new CreateJobOn(
            $"PN-{Guid.NewGuid():N}"[..12],
            "1075C142",
            machine.Id,
            null,
            "T-MF",
            null,
            null,
            null,
            caloteMm));
        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        return (machine, outcome.JobOn!.JobOnId);
    }

    [Fact]
    public async Task Create_ComCalote_GravaNoJobOn()
    {
        // A calote da ficha grava na criação (a coluna aditiva da B1,
        // null por defeito).
        var (machine, _) = await SeedProducaoAsync();
        var outcome = await _create.HandleAsync(new CreateJobOn(
            "PN-CALOTE-1",
            "1075C142",
            machine.Id,
            null,
            "T-MF",
            null,
            null,
            null,
            9.5m));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "PN-CALOTE-1");
        Assert.Equal(9.5m, jobOn.CaloteMm);
    }

    [Fact]
    public async Task Create_SemCalote_NullPorDefeito()
    {
        // O html: «Usar referência da Tool» — vazio = null (o apoio
        // TP/calote do Peso usa a referência automaticamente).
        var (machine, _) = await SeedProducaoAsync();
        var outcome = await _create.HandleAsync(new CreateJobOn(
            "PN-SEM-CALOTE", "1075C142", machine.Id, null, "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "PN-SEM-CALOTE");
        Assert.Null(jobOn.CaloteMm);
    }

    [Fact]
    public async Task Create_CaloteNegativa_RecusaTipadaSemEscrita()
    {
        var (machine, _) = await SeedProducaoAsync();

        var outcome = await _create.HandleAsync(new CreateJobOn(
            "PN-CALOTE-NEGA", "1075C142", machine.Id, null, "T-MF", null, null, null, -1m));

        Assert.Equal(CreateJobOnResult.InvalidCalote, outcome.Result);
        Assert.Null(outcome.JobOn);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync(j => j.ProductionNumber == "PN-CALOTE-NEGA"));
    }

    [Fact]
    public async Task Update_CaloteEditavel_GravaOValorSubmetido()
    {
        // A ficha edita a calote: o campo vem preenchido com o valor
        // corrente e o valor submetido grava sempre.
        var (machine, jobOnId) = await SeedProducaoAsync(caloteMm: 9.5m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, null, "T-MF", null, null, null, 8m));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal(8m, jobOn.CaloteMm);
    }

    [Fact]
    public async Task Update_CaloteLimpa_VoltaAReferenciaDaTool()
    {
        // Limpar o campo = null = a referência da Tool manda (o html:
        // «Usar referência da Tool»).
        var (machine, jobOnId) = await SeedProducaoAsync(caloteMm: 9.5m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, null, "T-MF", null, null, null, null));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Null(jobOn.CaloteMm);
    }

    [Fact]
    public async Task Update_CaloteZero_Valida_CalotePlana()
    {
        // 0 é válido (calote plana — o cap com h=0: R indefinido, volume 0).
        var (machine, jobOnId) = await SeedProducaoAsync();

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, null, "T-MF", null, null, null, 0m));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal(0m, jobOn.CaloteMm);
    }

    [Fact]
    public async Task Update_CaloteNegativa_RecusaTipadaSemEscrita()
    {
        var (machine, jobOnId) = await SeedProducaoAsync(caloteMm: 9.5m);

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, null, "T-MF", null, null, null, -3m));

        Assert.Equal(UpdateJobOnResult.InvalidCalote, outcome.Result);
        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal(9.5m, jobOn.CaloteMm); // a recusa não altera o facto persistido
    }

    [Fact]
    public async Task ContextoReadModel_TrazACalote()
    {
        // O read model canónico do contexto (o mesmo que os módulos
        // seguintes consomem) transporta a calote para a ficha.
        var (machine, jobOnId) = await SeedProducaoAsync(caloteMm: 9.5m);

        var query = new ProductionContextQuery(_db);
        var readModel = await query.GetAsync(jobOnId);

        Assert.NotNull(readModel);
        Assert.Equal(9.5m, readModel!.CaloteMm);
    }
}

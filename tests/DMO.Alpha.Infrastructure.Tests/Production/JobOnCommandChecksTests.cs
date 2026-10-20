using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Sweep de 19/10/2026, J-4: o Criar e o Editar do Job On verificam a
/// máquina, o MF obrigatório e as ferramentas no mesmo sítio, com uma só
/// consulta às ferramentas. As recusas e a ordem delas são as de antes, e
/// iguais nos dois.
/// </summary>
public sealed class JobOnCommandChecksTests : IDisposable
{
    private readonly DmoDbContext _db;
    private int _machineId;
    private int _jobOnId;

    public JobOnCommandChecksTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync()
    {
        var machine = new Machine { Code = "J4" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "T-CM", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "T-MF", Type = ToolType.Mf, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "T-BQ", Type = ToolType.Bq, Reference = "5447T173", Lot = "1" });
        await _db.SaveChangesAsync();
        _machineId = machine.Id;

        var created = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn("100", "5447T173", _machineId, MfToolId: "T-MF"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        _jobOnId = await _db.JobOns.Select(j => j.Id).SingleAsync();
        _db.ChangeTracker.Clear();
    }

    public static TheoryData<string?, string?, string?, string> Ferramentas => new()
    {
        { "T-CM", "T-MF", "T-BQ", "Success" },
        { null, "T-MF", null, "Success" },
        { "T-MF", "T-MF", "T-MF", "Success" },
        { "X", "T-MF", "Y", "CmToolNotFound" },
        { "", "T-MF", null, "CmToolNotFound" },
        { ToolRegistryConstants.RemovedToolId, "T-MF", null, "CmToolNotFound" },
        { "T-CM", "X", "Y", "MfToolNotFound" },
        { "T-CM", ToolRegistryConstants.RemovedToolId, null, "MfToolNotFound" },
        { "T-CM", "T-MF", "Y", "BqToolNotFound" },
        { null, "T-MF", "", "BqToolNotFound" },
        { "X", null, "Y", "MissingMfTool" },
        { "X", "", "Y", "MissingMfTool" }
    };

    [Theory]
    [MemberData(nameof(Ferramentas))]
    public async Task Criar_VerificaAsFerramentasPelaOrdemDeAntes(string? cm, string? mf, string? bq, string esperado)
    {
        await SeedAsync();

        var outcome = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn("200", "5447T173", _machineId, cm, mf, bq));

        Assert.Equal(esperado, outcome.Result.ToString());
    }

    [Theory]
    [MemberData(nameof(Ferramentas))]
    public async Task Editar_VerificaAsFerramentasComoOCriar(string? cm, string? mf, string? bq, string esperado)
    {
        await SeedAsync();

        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(_jobOnId, _machineId, cm, mf, bq));

        Assert.Equal(esperado, outcome.Result.ToString());
    }

    [Fact]
    public async Task SemMaquina_AMaquinaVemAntesDoMfEmFalta_NoCriarENoEditar()
    {
        await SeedAsync();

        var criar = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn("201", "5447T173", _machineId + 99, "X", null, "Y"));
        var editar = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(_jobOnId, _machineId + 99, "X", null, "Y"));

        Assert.Equal(CreateJobOnResult.MachineNotFound, criar.Result);
        Assert.Equal(UpdateJobOnResult.MachineNotFound, editar.Result);
    }
}

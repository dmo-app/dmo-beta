using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova o read path das máquinas persistidas para seleção humana
/// explícita no Job On: projeção dos factos reais ordenada por code e
/// lista vazia como estado truthful de um registo ainda não povoado.
/// </summary>
public sealed class MachineListQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly MachineListQuery _query;

    public MachineListQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new MachineListQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ListAsync_ReturnsPersistedMachinesOrderedByCode()
    {
        _db.Machines.Add(new Machine { Code = "B2" });
        _db.Machines.Add(new Machine { Code = "B1" });
        await _db.SaveChangesAsync();

        var result = await _query.ListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("B1", result[0].Code);
        Assert.Equal("B2", result[1].Code);
        Assert.Contains(result, m => m.MachineId > 0);
    }

    [Fact]
    public async Task ListAsync_NoMachinesRegistered_ReturnsEmptyList()
    {
        var result = await _query.ListAsync();

        Assert.Empty(result);
    }
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// A folha de Pegamentos sem produção (varredura de 18/10/2026, B5): o mesmo
/// envio repetido abre a folha vazia que o primeiro criou.
/// </summary>
public sealed class CreateToolScopedPegamentosHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateToolScopedPegamentosHandler _handler;

    public CreateToolScopedPegamentosHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CreateToolScopedPegamentosHandler(_db, new PegamentosToleranceConfiguration(_db));
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = "MF-2", Type = ToolType.Mf, Reference = "5811", Lot = "1" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RepeatedClicks_OpenTheSameEmptySheet_NewOneOnlyAfterMeasurements()
    {
        var first = await _handler.HandleAsync(new CreateToolScopedPegamentos("CM-1", "BQ-1", "MF-1"));
        var again = await _handler.HandleAsync(new CreateToolScopedPegamentos(" CM-1", "BQ-1 ", "MF-1"));
        Assert.Equal(CreateToolScopedPegamentosResult.Success, again.Result);
        Assert.Equal(first.PegamentosId, again.PegamentosId);

        // Outro MF é outra folha.
        var otherMf = await _handler.HandleAsync(new CreateToolScopedPegamentos("CM-1", "BQ-1", "MF-2"));
        Assert.NotEqual(first.PegamentosId, otherMf.PegamentosId);

        // Com medições gravadas, o mesmo envio começa uma folha nova.
        _db.PegamentosMeasurements.Add(new PegamentosMeasurement
        {
            PegamentosId = first.PegamentosId!.Value,
            RowPosition = 1,
            Component = PegamentosComponent.Cm,
            CosturaMm = 70m
        });
        await _db.SaveChangesAsync();
        var next = await _handler.HandleAsync(new CreateToolScopedPegamentos("CM-1", "BQ-1", "MF-1"));
        Assert.NotEqual(first.PegamentosId, next.PegamentosId);
        Assert.Equal(3, await _db.Pegamentos.CountAsync());
    }
}

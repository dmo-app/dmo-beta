using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// A escrita das ENTIDADES REPARADORAS (o vocabulário do encaminhamento
/// por máquina — o Owner 2026-10-12: «reparadores sao as entidades para
/// onde enviamos as boquilhas»): criar com o nome (o fato humano; o Id é
/// emitido pelo backend), recusas tipadas informativas no nome vazio e no
/// nome repetido; editar o nome SEM reescrever os movimentos passados (o
/// congelado de cada saída é a verdade histórica dela).
/// </summary>
public sealed partial class BqRepairerWriteHandlersTests
{
    private static DmoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-bq-repairer-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new DmoDbContext(options);
    }

    [Fact]
    public async Task Create_WithTheName_SavesAndEmitsTheId()
    {
        using var db = CreateContext();
        var handler = new CreateBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new CreateBqRepairer("Vidroart"));

        Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
        Assert.NotNull(outcome.BqRepairerId);
        Assert.Equal("Vidroart", outcome.Name);
        var persisted = await db.BqRepairers.AsNoTracking()
            .SingleAsync(r => r.Id == outcome.BqRepairerId);
        Assert.Equal("Vidroart", persisted.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyName_RefusesTyped(string? name)
    {
        using var db = CreateContext();
        var handler = new CreateBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new CreateBqRepairer(name ?? string.Empty));

        Assert.Equal(BqRepairerWriteResult.EmptyName, outcome.Result);
        Assert.Empty(await db.BqRepairers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Create_NameAlreadyInUse_RefusesTyped()
    {
        // Dois reparadores com o mesmo nome seriam o mesmo fato humano
        // para a rota — a recusa é tipada (o índice único é a última linha
        // da persistência).
        using var db = CreateContext();
        db.BqRepairers.Add(new BqRepairer { Name = "Vidroart" });
        await db.SaveChangesAsync();
        var handler = new CreateBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new CreateBqRepairer("Vidroart"));

        Assert.Equal(BqRepairerWriteResult.NameAlreadyInUse, outcome.Result);
        Assert.Equal(1, await db.BqRepairers.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_NameIsTrimmed_BeforeSaving()
    {
        // O nome grava tal como o operador o confirmou, sem o espaço
        // acidental do input.
        using var db = CreateContext();
        var handler = new CreateBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new CreateBqRepairer("  Vidroart  "));

        Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
        Assert.Equal("Vidroart", outcome.Name);
    }

    [Fact]
    public async Task Edit_ChangesTheName_WithoutTouchingTheFrozenMovements()
    {
        // A EDIÇÃO não reescreve os movimentos passados: o NOME CONGELADO
        // de cada saída é a verdade histórica DELA (blueprint L372-374) —
        // só as saídas FUTURAS congelam o nome novo.
        using var db = CreateContext();
        var repairer = new BqRepairer { Name = "Vidroart" };
        db.BqRepairers.Add(repairer);
        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9" };
        db.BqRepairTraces.Add(trace);
        db.BqMovements.Add(new BqMovement
        {
            BqRepairTraceId = trace.Id,
            Type = BqMovementType.Saida,
            Quantity = 2,
            BqRepairerId = repairer.Id,
            BqRepairerNameFrozen = "Vidroart"
        });
        await db.SaveChangesAsync();
        var handler = new EditBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(
            new EditBqRepairer(repairer.Id, "Boquilharia Central"));

        Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
        var renamed = await db.BqRepairers.AsNoTracking()
            .SingleAsync(r => r.Id == repairer.Id);
        Assert.Equal("Boquilharia Central", renamed.Name);

        // O movimento antigo mantém o nome congelado do momento dele.
        var movement = await db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.BqRepairTraceId == trace.Id);
        Assert.Equal(repairer.Id, movement.BqRepairerId);
        Assert.Equal("Vidroart", movement.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Edit_MissingRepairer_RefusesTyped()
    {
        using var db = CreateContext();
        var handler = new EditBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new EditBqRepairer(9999, "Vidroart"));

        Assert.Equal(BqRepairerWriteResult.NotFound, outcome.Result);
    }

    [Fact]
    public async Task Edit_NameAlreadyUsedByAnotherRepairer_RefusesTyped()
    {
        using var db = CreateContext();
        var first = new BqRepairer { Name = "Vidroart" };
        var second = new BqRepairer { Name = "Boquilharia Central" };
        db.BqRepairers.AddRange(first, second);
        await db.SaveChangesAsync();
        var handler = new EditBqRepairerHandler(db);

        var outcome = await handler.HandleAsync(new EditBqRepairer(second.Id, "Vidroart"));

        Assert.Equal(BqRepairerWriteResult.NameAlreadyInUse, outcome.Result);
        var unchanged = await db.BqRepairers.AsNoTracking()
            .SingleAsync(r => r.Id == second.Id);
        Assert.Equal("Boquilharia Central", unchanged.Name);
    }
}

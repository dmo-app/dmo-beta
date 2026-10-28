using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Os AN dos movimentos (Diogo, 28/10/2026: «vamos passar a registar AN»):
/// gravam-se com o movimento, opcionais; a correção só os muda quando os
/// traz; a quantidade negativa é recusada e nada é gravado.
/// </summary>
public sealed class BqMovementAnTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;

    public BqMovementAnTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task<BqRepairTrace> SeedTraceAsync()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-AN-1", Type = ToolType.Bq, Reference = "AN173", Lot = "1" });
        var trace = new BqRepairTrace { ToolId = "BQ-AN-1" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    private RegisterBqMovementHandler Register() => new(_db, new SemDefinicoes());

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class SemDefinicoes : DMO.Alpha.Core.Controlo.IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task Registar_ComAn_GravaOsAn_SemAnOuZeroFicaSemAn()
    {
        var trace = await SeedTraceAsync();

        Assert.Equal(RegisterBqMovementResult.Success,
            (await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 4, AnQuantity: 6))).Result);
        Assert.Equal(RegisterBqMovementResult.Success,
            (await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 1))).Result);
        Assert.Equal(RegisterBqMovementResult.Success,
            (await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 1, AnQuantity: 0))).Result);

        var an = await _db.BqMovements.AsNoTracking().OrderBy(m => m.Id).Select(m => m.AnQuantity).ToListAsync();
        Assert.Equal([6, null, null], an);
    }

    [Fact]
    public async Task Registar_AnNegativo_RecusaENadaGrava()
    {
        var trace = await SeedTraceAsync();

        var outcome = await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 4, AnQuantity: -1));

        Assert.Equal(RegisterBqMovementResult.InvalidAnQuantity, outcome.Result);
        Assert.Empty(await _db.BqMovements.ToListAsync());
    }

    [Fact]
    public async Task Corrigir_SoMudaOsAnQuandoOsTraz()
    {
        var trace = await SeedTraceAsync();
        Assert.Equal(RegisterBqMovementResult.Success,
            (await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 4, AnQuantity: 6))).Result);
        var id = (await _db.BqMovements.AsNoTracking().SingleAsync()).Id;
        var edit = new EditBqMovementHandler(_db, new FixedClock(Now));

        // Sem os AN no pedido, ficam como estavam.
        Assert.Equal(EditBqMovementResult.Success, (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 5))).Result);
        Assert.Equal(6, (await _db.BqMovements.AsNoTracking().SingleAsync()).AnQuantity);

        // Com os AN, mudam; em branco, o movimento fica sem AN.
        Assert.Equal(EditBqMovementResult.Success, (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 5, AnQuantity: 8, ChangesAn: true))).Result);
        Assert.Equal(8, (await _db.BqMovements.AsNoTracking().SingleAsync()).AnQuantity);
        Assert.Equal(EditBqMovementResult.Success, (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 5, AnQuantity: null, ChangesAn: true))).Result);
        Assert.Null((await _db.BqMovements.AsNoTracking().SingleAsync()).AnQuantity);

        Assert.Equal(EditBqMovementResult.InvalidAnQuantity,
            (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 5, AnQuantity: -2, ChangesAn: true))).Result);
    }

    [Fact]
    public async Task Corrigir_SoAn_AceitaZeroBq_SemAnRecusa()
    {
        // «Registar sem Job On» com a peça AN grava 0 BQ (Diogo, 28/10/2026).
        var trace = await SeedTraceAsync();
        Assert.Equal(RegisterBqMovementResult.Success,
            (await Register().HandleAsync(new RegisterBqMovement(trace.Id, "saida", 0, AnQuantity: 5))).Result);
        var id = (await _db.BqMovements.AsNoTracking().SingleAsync()).Id;
        var edit = new EditBqMovementHandler(_db, new FixedClock(Now));

        Assert.Equal(EditBqMovementResult.Success,
            (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 0, AnQuantity: 6, ChangesAn: true))).Result);
        Assert.Equal(EditBqMovementResult.InvalidQuantity,
            (await edit.HandleAsync(new EditBqMovement(trace.Id, id, "saida", 0, AnQuantity: null, ChangesAn: true))).Result);
        Assert.Equal(6, (await _db.BqMovements.AsNoTracking().SingleAsync()).AnQuantity);
    }
}

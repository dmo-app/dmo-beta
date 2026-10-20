using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Decisão do proprietário de 16/10/2026: abrir a máquina não cria o registo
/// de reparação; o registo nasce com o primeiro movimento. Prova o
/// HandleForContextAsync: o primeiro movimento cria um registo já associado
/// ao bq_id (a mesma ancoragem do fluxo antigo: âncora direta limpa, a
/// referência e o lote congelados), o segundo reutiliza-o, o registo
/// pré-produção da mesma boquilha é associado com os seus movimentos e uma
/// recusa não deixa registo nenhum.
/// </summary>
public sealed class RegisterBqMovementForContextTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly RegisterBqMovementHandler _handler;

    private sealed class NoSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    public RegisterBqMovementForContextTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RegisterBqMovementHandler(_db, new NoSettings());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<BqContext> SeedProductionAsync(string toolId)
    {
        _db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "9389T194", Lot = "12" });
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = "PN-202616",
            Machine = new Machine { Code = "B1" }
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = toolId };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return bq;
    }

    [Fact]
    public async Task FirstMovement_CreatesOneAssociatedTrace_AndTheMovement()
    {
        var bq = await SeedProductionAsync("BQ-CTX-1");
        Assert.Equal(0, await _db.BqRepairTraces.CountAsync());

        var outcome = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "saida", 3));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Equal(bq.Id, trace.BqContextId);
        Assert.Null(trace.ToolId);
        Assert.Equal("9389T194", trace.ToolReferenceFrozen);
        Assert.Equal("12", trace.ToolLotFrozen);
        Assert.Equal(trace.Id, outcome.Movement!.BqRepairTraceId);

        var movement = await _db.BqMovements.AsNoTracking().SingleAsync();
        Assert.Equal(trace.Id, movement.BqRepairTraceId);
        Assert.Equal(BqMovementType.Saida, movement.Type);
        Assert.Equal(3, movement.Quantity);
        // A máquina da produção resolve como no registo normal.
        Assert.Equal("B1", movement.MachineCodeFrozen);
    }

    [Fact]
    public async Task SecondMovement_ReusesTheTrace()
    {
        var bq = await SeedProductionAsync("BQ-CTX-2");

        var first = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "saida", 5));
        var second = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "entrada", 7));

        Assert.Equal(RegisterBqMovementResult.Success, second.Result);
        Assert.Equal(first.Movement!.BqRepairTraceId, second.Movement!.BqRepairTraceId);
        Assert.Equal(1, await _db.BqRepairTraces.CountAsync());
        Assert.Equal(2, await _db.BqMovements.CountAsync());
        // O saldo continua a ser calculado no mesmo registo: 7 entram, 5 saíram.
        Assert.Equal(-2, second.Movement.Discrepancy);
    }

    [Fact]
    public async Task UnresolvedTraceOfTheSameTool_IsAssociatedWithItsMovements()
    {
        var bq = await SeedProductionAsync("BQ-CTX-3");
        var unresolved = new BqRepairTrace { ToolId = "BQ-CTX-3" };
        _db.BqRepairTraces.Add(unresolved);
        await _db.SaveChangesAsync();
        var earlier = await _handler.HandleAsync(new RegisterBqMovement(unresolved.Id, "saida", 2));
        Assert.Equal(RegisterBqMovementResult.Success, earlier.Result);
        _db.ChangeTracker.Clear();

        var outcome = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "entrada", 2));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(unresolved.Id, outcome.Movement!.BqRepairTraceId);
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Equal(bq.Id, trace.BqContextId);
        Assert.Null(trace.ToolId);
        Assert.Equal(2, await _db.BqMovements.CountAsync(m => m.BqRepairTraceId == unresolved.Id));
        Assert.Null(outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task RefusedMovement_LeavesNoTrace()
    {
        var bq = await SeedProductionAsync("BQ-CTX-4");

        var outcome = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "desconhecido", 1));

        Assert.Equal(RegisterBqMovementResult.UnknownMovementType, outcome.Result);
        await _db.SaveChangesAsync();
        Assert.Equal(0, await _db.BqRepairTraces.CountAsync());
        Assert.Equal(0, await _db.BqMovements.CountAsync());
    }

    [Fact]
    public async Task RefusedMovement_KeepsTheUnresolvedTraceUnassociated()
    {
        var bq = await SeedProductionAsync("BQ-CTX-5");
        var unresolved = new BqRepairTrace { ToolId = "BQ-CTX-5" };
        _db.BqRepairTraces.Add(unresolved);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var outcome = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "saida", 1, MachineId: 999_999));

        Assert.Equal(RegisterBqMovementResult.MachineNotFound, outcome.Result);
        await _db.SaveChangesAsync();
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Null(trace.BqContextId);
        Assert.Equal("BQ-CTX-5", trace.ToolId);
    }

    [Fact]
    public async Task FutureRealDate_IsSaved_AndSaysSo()
    {
        // Diogo, 19/10/2026: «as datas têm de ser livres». O primeiro
        // movimento com a data real no futuro cria o registo e grava; o
        // outcome diz que a data está no futuro, para a página avisar.
        var bq = await SeedProductionAsync("BQ-CTX-6");
        var now = new DateTime(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc);
        var handler = new RegisterBqMovementHandler(_db, new NoSettings(), new FixedClock(now));

        var outcome = await handler.HandleForContextAsync(
            new RegisterBqMovementForContext(bq.Id, "saida", 1, OccurredAtUtc: now.AddDays(1)));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.True(outcome.OccurredInTheFuture);
        Assert.Equal(1, await _db.BqRepairTraces.CountAsync());
        Assert.Equal(now.AddDays(1), (await _db.BqMovements.AsNoTracking().SingleAsync()).OccurredAtUtc);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    [Fact]
    public async Task UnknownContext_IsRefusedAsTraceNotFound()
    {
        var outcome = await _handler.HandleForContextAsync(
            new RegisterBqMovementForContext(424242, "saida", 1));

        Assert.Equal(RegisterBqMovementResult.TraceNotFound, outcome.Result);
        Assert.Equal(0, await _db.BqRepairTraces.CountAsync());
    }
}

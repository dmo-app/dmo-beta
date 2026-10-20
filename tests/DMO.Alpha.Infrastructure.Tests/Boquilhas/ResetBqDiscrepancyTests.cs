using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Pôr a discrepância a zero (Diogo, 17/10/2026): grava quem e quando, não
/// mexe nos movimentos, e a discrepância acumulada passa a contar só os
/// movimentos com data real depois desse momento.
/// </summary>
public sealed class ResetBqDiscrepancyTests : IDisposable
{
    private static readonly DateTime ResetAt = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;

    private sealed class NoSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    public ResetBqDiscrepancyTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedTraceAsync()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-RST", Type = ToolType.Bq, Reference = "T173", Lot = "7" });
        var trace = new BqRepairTrace { ToolId = "BQ-RST" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace.Id;
    }

    // Os movimentos depois do zero registam-se com o relógio um dia depois
    // dele, para não serem movimentos no futuro.
    private Task RegisterAsync(int traceId, string type, int quantity, DateTime occurredAtUtc) =>
        new RegisterBqMovementHandler(_db, new NoSettings(), new FixedClock(ResetAt.AddDays(1)))
            .HandleAsync(new RegisterBqMovement(traceId, type, quantity, OccurredAtUtc: occurredAtUtc));

    [Fact]
    public async Task Reset_SavesWhoAndWhen_AndKeepsTheMovements()
    {
        var traceId = await SeedTraceAsync();
        await RegisterAsync(traceId, "entrada", 4, ResetAt.AddDays(-1));

        var result = await new ResetBqDiscrepancyHandler(_db, new FixedClock(ResetAt))
            .HandleAsync(new ResetBqDiscrepancy(traceId, "  Diogo  "));

        Assert.Equal(ResetBqDiscrepancyResult.Success, result);
        var trace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Equal(ResetAt, trace.DiscrepancyResetAtUtc);
        Assert.Equal("Diogo", trace.DiscrepancyResetByNameFrozen);
        var movement = await _db.BqMovements.AsNoTracking().SingleAsync();
        Assert.Equal(-4, movement.Discrepancy);
        Assert.Equal(4, movement.Quantity);
    }

    [Fact]
    public async Task Reset_UnknownTrace_IsRefused()
    {
        var result = await new ResetBqDiscrepancyHandler(_db, new FixedClock(ResetAt))
            .HandleAsync(new ResetBqDiscrepancy(999));

        Assert.Equal(ResetBqDiscrepancyResult.TraceNotFound, result);
    }

    [Fact]
    public async Task AccumulatedDiscrepancy_CountsOnlyTheMovementsAfterTheReset()
    {
        var traceId = await SeedTraceAsync();
        await RegisterAsync(traceId, "entrada", 4, ResetAt.AddDays(-1));
        var query = new BoquilhasRegistoQuery(_db);
        Assert.Equal(-4, (await query.FindTraceAsync(traceId))!.AccumulatedDiscrepancy);

        await new ResetBqDiscrepancyHandler(_db, new FixedClock(ResetAt))
            .HandleAsync(new ResetBqDiscrepancy(traceId, "Diogo"));
        _db.ChangeTracker.Clear();

        var afterReset = (await query.FindTraceAsync(traceId))!;
        Assert.Equal(0, afterReset.AccumulatedDiscrepancy);
        Assert.Equal(ResetAt, afterReset.DiscrepancyResetAtUtc);
        Assert.Equal("Diogo", afterReset.DiscrepancyResetBy);

        // Um retorno depois do zero volta a contar.
        await RegisterAsync(traceId, "saida", 2, ResetAt.AddHours(1));
        await RegisterAsync(traceId, "entrada", 3, ResetAt.AddHours(2));
        _db.ChangeTracker.Clear();
        Assert.Equal(-1, (await query.FindTraceAsync(traceId))!.AccumulatedDiscrepancy);
    }

    // O campo da hora só tem minutos (teste do agente de 19/10/2026, T616eb4d):
    // pôr a zero às 10:20:56 e gravar logo a seguir com a hora pré-preenchida
    // (10:20) volta a contar, como num minuto seguinte.
    [Fact]
    public async Task AccumulatedDiscrepancy_CountsAMovementSavedAfterTheReset_InTheSameMinute()
    {
        var resetAt = new DateTime(2026, 10, 19, 10, 20, 56, DateTimeKind.Utc);
        var sameMinute = new DateTime(2026, 10, 19, 10, 20, 0, DateTimeKind.Utc);
        var traceId = await SeedTraceAsync();
        Task Register(string type, int quantity, DateTime occurredAt, DateTime savedAt) =>
            new RegisterBqMovementHandler(_db, new NoSettings(), new FixedClock(savedAt))
                .HandleAsync(new RegisterBqMovement(traceId, type, quantity, OccurredAtUtc: occurredAt));
        await Register("entrada", 4, sameMinute.AddMinutes(-5), resetAt.AddMinutes(-5));
        await new ResetBqDiscrepancyHandler(_db, new FixedClock(resetAt))
            .HandleAsync(new ResetBqDiscrepancy(traceId, "Diogo"));
        _db.ChangeTracker.Clear();
        var query = new BoquilhasRegistoQuery(_db);
        Assert.Equal(0, (await query.FindTraceAsync(traceId))!.AccumulatedDiscrepancy);

        await Register("saida", 2, sameMinute, resetAt.AddSeconds(10));
        await Register("entrada", 3, sameMinute, resetAt.AddSeconds(20));
        _db.ChangeTracker.Clear();
        Assert.Equal(-1, (await query.FindTraceAsync(traceId))!.AccumulatedDiscrepancy);
    }
}

using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova o fold da discrepância pela data real (depois o Id) e o caso do
/// backfill: uma saída com data antiga gravada depois explica a entrada já gravada.
/// </summary>
public sealed class BqDiscrepancyFoldTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;

    public BqDiscrepancyFoldTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private static BqMovement Movement(int id, BqMovementType type, int quantity, DateTime occurredAtUtc) =>
        new() { Id = id, Type = type, Quantity = quantity, OccurredAtUtc = occurredAtUtc, CreatedAtUtc = occurredAtUtc };

    [Fact]
    public void Apply_OrdersByRealDate_NotByInsertionOrder()
    {
        // A entrada foi gravada primeiro (Id 1) mas aconteceu depois da saída (Id 2).
        var entrada = Movement(1, BqMovementType.Entrada, 3, Today);
        var saida = Movement(2, BqMovementType.Saida, 3, Today.AddDays(-7));

        var outstanding = BqDiscrepancyFold.Apply([entrada, saida]);

        Assert.Null(entrada.Discrepancy);
        Assert.Null(saida.Discrepancy);
        Assert.Equal(0, outstanding);
    }

    [Fact]
    public void Apply_EqualDates_SaidaFirst_ThenIdAndTheUnsavedMovementGoesLast()
    {
        var unsaved = Movement(0, BqMovementType.Entrada, 4, Today);
        var saida = Movement(7, BqMovementType.Saida, 3, Today);
        var entrada = Movement(5, BqMovementType.Entrada, 2, Today);

        var outstanding = BqDiscrepancyFold.Apply([unsaved, saida, entrada]);

        // À mesma hora a saída conta primeiro, mesmo registada depois (Diogo,
        // 17/10/2026): saída 7 (+3), entrada 5 (2 de 3), novo (4 contra 1: -3).
        Assert.Null(saida.Discrepancy);
        Assert.Null(entrada.Discrepancy);
        Assert.Equal(-3, unsaved.Discrepancy);
        Assert.Equal(0, outstanding);
    }

    [Fact]
    public void Apply_DiogosExample_OutOfOrderGuias_SortByTheRealTime()
    {
        // 12:45 sai 20 e entra 15, registados primeiro; depois 08:21 sai 10 e
        // entra 14, do mesmo dia, com a entrada registada antes da saída.
        var at1245 = Today.AddHours(12).AddMinutes(45);
        var at0821 = Today.AddHours(8).AddMinutes(21);
        var saida1245 = Movement(1, BqMovementType.Saida, 20, at1245);
        var entrada1245 = Movement(2, BqMovementType.Entrada, 15, at1245);
        var entrada0821 = Movement(3, BqMovementType.Entrada, 14, at0821);
        var saida0821 = Movement(4, BqMovementType.Saida, 10, at0821);

        var outstanding = BqDiscrepancyFold.Apply([saida1245, entrada1245, entrada0821, saida0821]);

        // 08:21: sai 10, entram 14 (4 a mais); 12:45: sai 20, entram 15 (ficam 5 fora).
        Assert.Equal(-4, entrada0821.Discrepancy);
        Assert.Null(entrada1245.Discrepancy);
        Assert.Equal(5, outstanding);
    }

    [Fact]
    public void Apply_ClearsDiscrepancyOfSaidas_AndRecomputesEveryReturn()
    {
        var saida = Movement(1, BqMovementType.Saida, 5, Today.AddHours(-2));
        saida.Discrepancy = -9;
        var semReparacao = Movement(2, BqMovementType.EntradaSemReparacao, 3, Today.AddHours(-1));
        var entrada = Movement(3, BqMovementType.Entrada, 4, Today);
        entrada.Discrepancy = -99;

        var outstanding = BqDiscrepancyFold.Apply([saida, semReparacao, entrada]);

        Assert.Null(saida.Discrepancy);
        Assert.Null(semReparacao.Discrepancy);
        Assert.Equal(-2, entrada.Discrepancy);
        Assert.Equal(0, outstanding);
    }

    [Fact]
    public async Task Backfill_SaidaDaSemanaPassada_ExplicaAEntradaDeHoje()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-FOLD-1", Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
        var trace = new BqRepairTrace { ToolId = "BQ-FOLD-1" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        var register = new RegisterBqMovementHandler(_db, new EmptySettings());

        var entrada = await register.HandleAsync(new RegisterBqMovement(trace.Id, "entrada", 3, null, Today));
        Assert.Equal(-3, entrada.Movement!.Discrepancy);

        var saida = await register.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 3, null, Today.AddDays(-7)));
        Assert.Equal(RegisterBqMovementResult.Success, saida.Result);

        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == entrada.Movement.MovementId);
        Assert.Null(persisted.Discrepancy);

        var read = await new BoquilhasRegistoQuery(_db).FindTraceAsync(trace.Id);
        Assert.NotNull(read);
        Assert.Equal(0, read!.QuantityLegitimatelyOut);
        Assert.Null(read.AccumulatedDiscrepancy);
    }

    private sealed class EmptySettings : DMO.Alpha.Core.Controlo.IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    // Depois de posta a zero (T616eb4d, o campo da hora só tem minutos): conta
    // a data real depois do zero, ou o minuto do zero gravado depois dele.
    [Theory]
    [InlineData("10:21:00", "10:21:05", true)]   // minuto seguinte
    [InlineData("10:20:00", "10:21:05", true)]   // mesmo minuto, gravado depois do zero
    [InlineData("10:20:00", "10:20:30", false)]  // mesmo minuto, gravado antes do zero
    [InlineData("10:19:00", "10:21:05", false)]  // minuto anterior, gravado depois
    public void CountsAfterReset_UsesTheResetMinute_ForMovementsSavedAfterIt(string occurred, string saved, bool counts)
    {
        var resetAt = new DateTime(2026, 10, 19, 10, 20, 56, DateTimeKind.Utc);
        DateTime At(string time) => resetAt.Date + TimeSpan.Parse(time, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(counts, BqDiscrepancyFold.CountsAfterReset(At(occurred), At(saved), resetAt));
    }
}


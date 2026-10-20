using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova a correção e a eliminação de movimentos gravados: as discrepâncias do
/// registo são recalculadas pela data real; só se recusa o que partiria os dados.
/// </summary>
public sealed class BqMovementCorrectionHandlersTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly EditBqMovementHandler _edit;
    private readonly DeleteBqMovementHandler _delete;
    private readonly FixedClock _clock = new(Now);

    public BqMovementCorrectionHandlersTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _edit = new EditBqMovementHandler(_db, _clock);
        _delete = new DeleteBqMovementHandler(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private async Task<BqRepairTrace> SeedTraceAsync(string toolId = "BQ-CORR-1")
    {
        if (!await _db.Tools.AnyAsync(t => t.ToolId == toolId))
        {
            _db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
        }
        var trace = new BqRepairTrace { ToolId = toolId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    private async Task<BqMovement> SeedMovementAsync(
        BqRepairTrace trace, BqMovementType type, int quantity, DateTime occurredAtUtc, int? discrepancy = null,
        DateTime? createdAtUtc = null)
    {
        var movement = new BqMovement
        {
            BqRepairTraceId = trace.Id,
            Type = type,
            Quantity = quantity,
            Discrepancy = discrepancy,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = createdAtUtc ?? occurredAtUtc,
            MachineCodeFrozen = type == BqMovementType.Saida ? "B1" : null,
            BqRepairerId = null,
            BqRepairerNameFrozen = type == BqMovementType.Saida ? "Vidroart" : null,
            RegisteredByOperatorId = "4100"
        };
        _db.BqMovements.Add(movement);
        await _db.SaveChangesAsync();
        return movement;
    }

    private Task<BqMovement> ReadAsync(int id) =>
        _db.BqMovements.AsNoTracking().SingleAsync(m => m.Id == id);

    [Fact]
    public async Task Edit_Quantity_RecomputesTheReturnDiscrepancy()
    {
        var trace = await SeedTraceAsync();
        await SeedMovementAsync(trace, BqMovementType.Saida, 5, Base);
        var entrada = await SeedMovementAsync(trace, BqMovementType.Entrada, 7, Base.AddHours(1), -2);

        var outcome = await _edit.HandleAsync(new EditBqMovement(trace.Id, entrada.Id, "entrada", 5));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.Null(outcome.Movement!.Discrepancy);
        var persisted = await ReadAsync(entrada.Id);
        Assert.Equal(5, persisted.Quantity);
        Assert.Null(persisted.Discrepancy);
    }

    [Fact]
    public async Task Edit_Type_SaidaToEntrada_KeepsFrozenFactsAndRecomputes()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        var entrada = await SeedMovementAsync(trace, BqMovementType.Entrada, 3, Base.AddHours(1));

        var outcome = await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "entrada", 3));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        var edited = await ReadAsync(saida.Id);
        Assert.Equal(BqMovementType.Entrada, edited.Type);
        Assert.Equal(-3, edited.Discrepancy);
        // A máquina, o reparador, o operador e as datas técnicas ficam como estavam.
        Assert.Equal("B1", edited.MachineCodeFrozen);
        Assert.Equal("Vidroart", edited.BqRepairerNameFrozen);
        Assert.Equal("4100", edited.RegisteredByOperatorId);
        Assert.Equal(Base, edited.OccurredAtUtc);
        Assert.Equal(Base, edited.CreatedAtUtc);
        Assert.Equal(-3, (await ReadAsync(entrada.Id)).Discrepancy);
    }

    [Fact]
    public async Task Edit_RealDate_MovesTheSaidaAfterTheEntrada_AndTheEntradaBecomesUnexplained()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        var entrada = await SeedMovementAsync(trace, BqMovementType.Entrada, 3, Base.AddDays(1));

        // 2026-10-05 10:30 em Lisboa (UTC+1) = 09:30 UTC.
        var outcome = await _edit.HandleAsync(new EditBqMovement(
            trace.Id, saida.Id, "saida", 3, new DateTime(2026, 10, 5, 10, 30, 0)));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.Equal(new DateTime(2026, 10, 5, 9, 30, 0, DateTimeKind.Utc), (await ReadAsync(saida.Id)).OccurredAtUtc);
        Assert.Equal(-3, (await ReadAsync(entrada.Id)).Discrepancy);
    }

    [Fact]
    public async Task Edit_Refusals_LeaveTheMovementUntouched()
    {
        var trace = await SeedTraceAsync();
        var otherTrace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        Assert.Equal(EditBqMovementResult.MovementNotFound,
            (await _edit.HandleAsync(new EditBqMovement(otherTrace.Id, saida.Id, "saida", 4))).Result);
        Assert.Equal(EditBqMovementResult.MovementNotFound,
            (await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id + 999, "saida", 4))).Result);
        Assert.Equal(EditBqMovementResult.UnknownMovementType,
            (await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "reparado", 4))).Result);
        Assert.Equal(EditBqMovementResult.InvalidQuantity,
            (await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "saida", 0))).Result);

        var persisted = await ReadAsync(saida.Id);
        Assert.Equal(BqMovementType.Saida, persisted.Type);
        Assert.Equal(3, persisted.Quantity);
        Assert.Equal(Base, persisted.OccurredAtUtc);
    }

    [Fact]
    public async Task Edit_TimeInTheDstGap_MovesToTheNextHourAndReportsTheShift()
    {
        // 29/03/2026 01:30 não existe em Lisboa (01:00 passa a 02:00): a
        // correção grava às 02:30 (01:30 UTC) e devolve a hora ajustada para
        // a página avisar — já não recusa (decisão do proprietário, 16/10/2026).
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        var outcome = await _edit.HandleAsync(new EditBqMovement(
            trace.Id, saida.Id, "saida", 4, new DateTime(2026, 3, 29, 1, 30, 0)));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.Equal(new DateTime(2026, 3, 29, 2, 30, 0), outcome.OccurredAtShiftedLocal);
        var persisted = await ReadAsync(saida.Id);
        Assert.Equal(4, persisted.Quantity);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), persisted.OccurredAtUtc);
    }

    [Fact]
    public async Task Edit_ValidTime_ReportsNoShift()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        var outcome = await _edit.HandleAsync(new EditBqMovement(
            trace.Id, saida.Id, "saida", 3, new DateTime(2026, 3, 29, 3, 0, 0)));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.Null(outcome.OccurredAtShiftedLocal);
    }

    [Fact]
    public async Task Delete_RecentMovement_RemovesItAndRecomputes()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        var entrada = await SeedMovementAsync(trace, BqMovementType.Entrada, 5, Base.AddHours(1), -2);

        var result = await _delete.HandleAsync(new DeleteBqMovement(trace.Id, saida.Id));

        Assert.Equal(DeleteBqMovementResult.Success, result);
        Assert.False(await _db.BqMovements.AsNoTracking().AnyAsync(m => m.Id == saida.Id));
        Assert.Equal(-5, (await ReadAsync(entrada.Id)).Discrepancy);
    }

    [Fact]
    public async Task Delete_AnyMovement_EvenTheOldest_IsRemovedAndTheCountIsRecalculated()
    {
        // Guias em atraso (Diogo, 19/10/2026): apaga-se qualquer movimento,
        // mesmo com 5 ou mais de data real mais recente, e as discrepâncias
        // dos que ficam voltam a contar pela data real.
        var trace = await SeedTraceAsync();
        var oldSaida = await SeedMovementAsync(trace, BqMovementType.Saida, 2, Base.AddDays(-30), createdAtUtc: Base.AddDays(6));
        for (var i = 0; i < 5; i++)
        {
            await SeedMovementAsync(trace, BqMovementType.Saida, 1, Base.AddDays(i));
        }
        var entrada = await SeedMovementAsync(trace, BqMovementType.Entrada, 7, Base.AddDays(5));
        Assert.Null(entrada.Discrepancy);

        Assert.Equal(DeleteBqMovementResult.Success,
            await _delete.HandleAsync(new DeleteBqMovement(trace.Id, oldSaida.Id)));

        Assert.Equal(6, await _db.BqMovements.AsNoTracking().CountAsync(m => m.BqRepairTraceId == trace.Id));
        Assert.Equal(-2, (await ReadAsync(entrada.Id)).Discrepancy);
        Assert.True(await _db.BqMovementDeletions.AsNoTracking().AnyAsync(d => d.BqMovementId == oldSaida.Id));
    }

    [Fact]
    public async Task Delete_MovementOfAnotherTrace_IsRefused()
    {
        var trace = await SeedTraceAsync();
        var otherTrace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        Assert.Equal(DeleteBqMovementResult.MovementNotFound,
            await _delete.HandleAsync(new DeleteBqMovement(otherTrace.Id, saida.Id)));
        Assert.True(await _db.BqMovements.AsNoTracking().AnyAsync(m => m.Id == saida.Id));
    }

    [Fact]
    public async Task Edit_Success_RecordsWhoCorrectedAndWhen()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        Assert.Null(saida.CorrectedAtUtc);

        var outcome = await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "saida", 4, null, "  Ana Silva "));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        var edited = await ReadAsync(saida.Id);
        Assert.Equal("Ana Silva", edited.CorrectedByNameFrozen);
        Assert.Equal(Now, edited.CorrectedAtUtc);
        // O operador que registou continua a ser o do registo original.
        Assert.Equal("4100", edited.RegisteredByOperatorId);

        var read = await new BoquilhasRegistoQuery(_db).FindTraceAsync(trace.Id);
        var listed = Assert.Single(read!.Movements);
        Assert.Equal("Ana Silva", listed.CorrectedByNameFrozen);
        Assert.Equal(Now, listed.CorrectedAtUtc);
    }

    [Fact]
    public async Task Edit_WithoutIdentity_StillSaves_WithWhoLeftEmpty()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        var outcome = await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "saida", 2));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        var edited = await ReadAsync(saida.Id);
        Assert.Equal(2, edited.Quantity);
        Assert.Null(edited.CorrectedByNameFrozen);
        Assert.Equal(Now, edited.CorrectedAtUtc);
    }

    [Fact]
    public async Task Edit_Refused_DoesNotRecordACorrection()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "saida", 0, null, "Ana Silva"));

        var persisted = await ReadAsync(saida.Id);
        Assert.Null(persisted.CorrectedByNameFrozen);
        Assert.Null(persisted.CorrectedAtUtc);
    }

    [Fact]
    public async Task Delete_Success_WritesTheDeletionTrail()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        var result = await _delete.HandleAsync(new DeleteBqMovement(trace.Id, saida.Id, "Rui Costa"));

        Assert.Equal(DeleteBqMovementResult.Success, result);
        var trail = await _db.BqMovementDeletions.AsNoTracking().SingleAsync();
        Assert.Equal(saida.Id, trail.BqMovementId);
        Assert.Equal(trace.Id, trail.BqRepairTraceId);
        Assert.Equal(BqMovementType.Saida, trail.Type);
        Assert.Equal(3, trail.Quantity);
        Assert.Equal(Base, trail.OccurredAtUtc);
        Assert.Equal("B1", trail.MachineCodeFrozen);
        Assert.Equal("Rui Costa", trail.DeletedByNameFrozen);
        Assert.Equal(Now, trail.DeletedAtUtc);
    }

    [Fact]
    public async Task Delete_WithoutIdentity_StillDeletes_WithWhoLeftEmpty()
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        Assert.Equal(DeleteBqMovementResult.Success,
            await _delete.HandleAsync(new DeleteBqMovement(trace.Id, saida.Id, " ")));

        Assert.False(await _db.BqMovements.AsNoTracking().AnyAsync(m => m.Id == saida.Id));
        var trail = await _db.BqMovementDeletions.AsNoTracking().SingleAsync();
        Assert.Null(trail.DeletedByNameFrozen);
        Assert.Equal(Now, trail.DeletedAtUtc);
    }

    [Fact]
    public async Task Delete_Refused_WritesNoTrail()
    {
        var trace = await SeedTraceAsync();
        var otherTrace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);

        await _delete.HandleAsync(new DeleteBqMovement(otherTrace.Id, saida.Id, "Rui Costa"));

        Assert.False(await _db.BqMovementDeletions.AsNoTracking().AnyAsync());
    }

    // Diogo, 19/10/2026: «as datas têm de ser livres». Corrigir a data real
    // para o futuro grava e o outcome diz-o, para a página avisar (na
    // beta.241 era recusado). O agora é 16/10/2026 14:30 UTC, 15:30 em
    // Lisboa; a folga é de 10 minutos.

    [Theory]
    [InlineData(2026, 10, 16, 15, 41)] // 11 minutos depois do agora
    [InlineData(2026, 10, 17, 8, 0)]   // amanhã
    public async Task Edit_RealDateInTheFutureBeyondTheTolerance_IsSaved_AndSaysSo(
        int year, int month, int day, int hour, int minute)
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        var local = new DateTime(year, month, day, hour, minute, 0);

        var outcome = await _edit.HandleAsync(new EditBqMovement(
            trace.Id, saida.Id, "entrada", 4, local, "Ana Silva"));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.True(outcome.OccurredInTheFuture);
        var persisted = await ReadAsync(saida.Id);
        Assert.Equal(BqMovementType.Entrada, persisted.Type);
        Assert.Equal(4, persisted.Quantity);
        Assert.Equal(DmoBusinessTime.ToUtc(local), persisted.OccurredAtUtc);
        Assert.Equal("Ana Silva", persisted.CorrectedByNameFrozen);
    }

    [Theory]
    [InlineData(2026, 10, 16, 15, 39)] // 9 minutos depois: o relógio do posto adiantado
    [InlineData(2026, 10, 16, 15, 40)] // o limite da folga ainda grava
    [InlineData(2025, 12, 1, 9, 0)]    // quase um ano antes: a guia lançada com atraso
    public async Task Edit_RealDateWithinTheToleranceOrInThePast_IsSaved(
        int year, int month, int day, int hour, int minute)
    {
        var trace = await SeedTraceAsync();
        var saida = await SeedMovementAsync(trace, BqMovementType.Saida, 3, Base);
        var local = new DateTime(year, month, day, hour, minute, 0);

        var outcome = await _edit.HandleAsync(new EditBqMovement(trace.Id, saida.Id, "saida", 3, local));

        Assert.Equal(EditBqMovementResult.Success, outcome.Result);
        Assert.False(outcome.OccurredInTheFuture);
        Assert.Equal(DmoBusinessTime.ToUtc(local), (await ReadAsync(saida.Id)).OccurredAtUtc);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}

using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Remover um reparador (Diogo, 21/10/2026) contra um PostgreSQL real: a FK
/// das saídas (Restrict) recusa apagá-lo diretamente; o handler larga a
/// ligação das saídas, guarda-lhes o nome e só depois o apaga, numa só
/// gravação.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task RemoverReparadorComSaidas_AsSaidasFicamComONome()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "RR-" + Guid.NewGuid().ToString("N")[..8];
        var repairer = new BqRepairer { Name = marker };
        db.BqRepairers.Add(repairer);
        var trace = new BqRepairTrace();
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 2, BqRepairerId = repairer.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        try
        {
            var direto = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"DELETE FROM \"BqRepairers\" WHERE \"Id\" = {repairer.Id}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, direto.SqlState);

            var outcome = await new DeleteBqRepairerHandler(db).HandleAsync(new DeleteBqRepairer(repairer.Id));

            Assert.Equal(BqRepairerWriteResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            Assert.False(await db.BqRepairers.AnyAsync(r => r.Id == repairer.Id));
            var saida = await db.BqMovements.AsNoTracking().SingleAsync(m => m.BqRepairTraceId == trace.Id);
            Assert.Null(saida.BqRepairerId);
            Assert.Equal(marker, saida.BqRepairerNameFrozen);
        }
        finally
        {
            await db.BqMovements.Where(m => m.BqRepairTraceId == trace.Id).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => t.Id == trace.Id).ExecuteDeleteAsync();
            await db.BqRepairers.Where(r => r.Id == repairer.Id).ExecuteDeleteAsync();
        }
    }
}

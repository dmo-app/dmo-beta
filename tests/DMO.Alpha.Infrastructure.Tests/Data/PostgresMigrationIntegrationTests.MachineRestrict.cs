using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migration JobOnMachineRestrict (varredura de 18/10/2026, F8): apagar uma
/// máquina que tem produções é recusado pela base, em vez de apagar em
/// cascata as produções e tudo o que pende delas.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task DeletingAMachineWithProductions_IsRefused_TheProductionStays()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "MR-" + Guid.NewGuid().ToString("N")[..8];
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"DELETE FROM \"Machines\" WHERE \"Id\" = {machineId}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refused.SqlState);
            Assert.True(await db.JobOns.AnyAsync(j => j.Id == jobOnId));
            Assert.True(await db.Machines.AnyAsync(m => m.Id == machineId));
        }
        finally
        {
            await DeleteJobOnAsync(db, machineId);
        }
    }
}

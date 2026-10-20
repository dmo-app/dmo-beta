using DMO.Alpha.Infrastructure.Tests.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O filtro da sobreposição em SQL (varredura de 18/10/2026, C2) dá no
/// PostgreSQL o mesmo aviso que a calculadora com todas as produções.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Overlap_FilteredInSql_GivesTheSameWarningAsComparingEveryProduction()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var prefix = "OV" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            await JobOnOverlapScenario.AssertSameAsCalculatorAsync(db, prefix);
        }
        finally
        {
            await db.JobOns.Where(j => j.Reference == prefix + "REF").ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Code.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }
}

using DMO.Alpha.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O índice da referência das ferramentas em maiúsculas (migração
/// ToolsReferenciaMaiusculas, vistoria BD-04, Diogo 27/10/2026) contra um
/// PostgreSQL real: a procura igual e a do início (LIKE 'X%') sobre
/// upper("Reference") usam-no, e encontram a referência escrita em minúsculas.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ToolsReferenciaMaiusculas_AsProcurasSemOlharAMaiusculasUsamOIndice()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "rm" + Guid.NewGuid().ToString("N")[..8];
        var toolId = "TRM-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            var upper = marker.ToUpperInvariant();
            Assert.Equal([toolId], await db.Tools.AsNoTracking()
                .Where(t => t.Reference.ToUpper() == upper).Select(t => t.ToolId).ToListAsync());

            await using var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var off = new Npgsql.NpgsqlCommand("SET LOCAL enable_seqscan = off", connection, transaction))
            {
                await off.ExecuteNonQueryAsync();
            }

            foreach (var where in new[] { $"upper(\"Reference\") = '{upper}'", $"upper(\"Reference\") LIKE '{upper[..4]}%'" })
            {
                await using var explain = new Npgsql.NpgsqlCommand($"EXPLAIN SELECT \"ToolId\" FROM \"Tools\" WHERE {where}", connection, transaction);
                var plan = new List<string>();
                await using (var reader = await explain.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        plan.Add(reader.GetString(0));
                    }
                }

                Assert.Contains(plan, line => line.Contains("IX_Tools_ReferenciaMaiusculas", StringComparison.Ordinal));
            }

            await transaction.RollbackAsync();
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
        }
    }
}

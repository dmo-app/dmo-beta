using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Sweep de 19/10/2026, C-4: os lotes de uma referência, do mais alto ao
/// «s/lote», traduzidos no PostgreSQL. O InMemory ordena em C# e não apanha
/// uma ordenação que o Npgsql não traduz. A referência tem algarismos ao
/// acaso, para a base partilhada pelos testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task LotesDaReferencia_SemLoteEmUltimo_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var referencia = $"T{p}";
        var ids = new[] { $"LOTE-SL-{p}", $"LOTE-9-{p}", $"LOTE-10-{p}", $"LOTE-CM-{p}" };
        db.Tools.AddRange(
            new Tool { ToolId = ids[0], Type = ToolType.Bq, Reference = referencia, Lot = ToolRegistryConstants.NoLot },
            new Tool { ToolId = ids[1], Type = ToolType.Bq, Reference = referencia, Lot = "9" },
            new Tool { ToolId = ids[2], Type = ToolType.Bq, Reference = referencia, Lot = "10" },
            new Tool { ToolId = ids[3], Type = ToolType.Cm, Reference = referencia, Lot = "99" });
        await db.SaveChangesAsync();

        try
        {
            var lotes = await ToolLookupQuery.ListByReferenceAsync(db, referencia.ToLowerInvariant(), ToolType.Bq);
            Assert.Equal(new[] { ids[2], ids[1], ids[0] }, lotes.Select(t => t.ToolId).ToArray());
            Assert.Equal(ids[2], (await new ToolLookupQuery(db).FindByReferenceAsync(referencia, ToolType.Bq))?.ToolId);
        }
        finally
        {
            await db.Tools.Where(t => ids.Contains(t.ToolId)).ExecuteDeleteAsync();
        }
    }
}

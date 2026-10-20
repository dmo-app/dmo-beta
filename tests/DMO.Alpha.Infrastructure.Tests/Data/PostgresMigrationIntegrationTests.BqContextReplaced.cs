using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migration BqContextReplaced (Diogo, 18/10/2026): trocar a BQ no Editar
/// do Job On grava um bq_id novo num PostgreSQL real; o anterior fica com a
/// BQ antiga, ligado à produção.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task SwappingTheBq_CreatesANewBqId_TheOldOneKeepsTheOldBq()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "BR-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T194", Lot = "2" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ1");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
            var oldBqId = await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();

            var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF", marker + "-BQ2"));

            Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            var current = await db.BqContexts.SingleAsync(c => c.JobOnId == jobOnId);
            Assert.NotEqual(oldBqId, current.Id);
            Assert.Equal(marker + "-BQ2", current.ToolId);
            var replaced = await db.BqContexts.SingleAsync(c => c.Id == oldBqId);
            Assert.Null(replaced.JobOnId);
            Assert.Equal(jobOnId, replaced.ReplacedJobOnId);
            Assert.Equal(marker + "-BQ1", replaced.ToolId);

            // Voltar à BQ1 retoma o bq_id dela: o índice único do JobOnId
            // aceita a troca na mesma escrita.
            var back = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF", marker + "-BQ1"));

            Assert.Equal(UpdateJobOnResult.Success, back.Result);
            db.ChangeTracker.Clear();
            Assert.Equal(oldBqId, await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync());
            Assert.Equal(2, await db.BqContexts.CountAsync(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task TrocarABqTresVezes_CadaUmaFicaNoSeuBqId_EVoltarAUmaRetomaOBqIdDela()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "B3-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T194", Lot = "2" },
            new Tool { ToolId = marker + "-BQ3", Type = ToolType.Bq, Reference = "T194", Lot = "3" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ1");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
            async Task<int> SwapBqAsync(string bq)
            {
                var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, marker + "-CM", marker + "-MF", marker + bq));
                Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
                db.ChangeTracker.Clear();
                return await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            }

            // Com a BQ1 e a BQ2 já usadas, a BQ3 nasce num bq_id novo e cada
            // bq_id anterior continua com a sua BQ.
            var bq1Id = await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            var bq2Id = await SwapBqAsync("-BQ2");
            var bq3Id = await SwapBqAsync("-BQ3");
            var bqs = await db.BqContexts
                .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                .OrderBy(c => c.Id)
                .ToListAsync();
            Assert.Equal(new[] { marker + "-BQ1", marker + "-BQ2", marker + "-BQ3" }, bqs.Select(c => c.ToolId));
            Assert.Equal(new[] { bq1Id, bq2Id, bq3Id }, bqs.Select(c => c.Id));
            Assert.Equal(new int?[] { null, null, jobOnId }, bqs.Select(c => c.JobOnId));

            // Voltar à BQ2, com a BQ1 e a BQ3 trocadas, retoma o bq_id da BQ2.
            Assert.Equal(bq2Id, await SwapBqAsync("-BQ2"));
            Assert.Equal(
                new[] { marker + "-BQ1", marker + "-BQ2", marker + "-BQ3" },
                await db.BqContexts
                    .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                    .OrderBy(c => c.Id)
                    .Select(c => c.ToolId)
                    .ToListAsync());
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }
}

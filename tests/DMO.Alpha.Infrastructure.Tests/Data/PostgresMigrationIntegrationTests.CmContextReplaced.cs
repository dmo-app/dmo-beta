using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migration CmContextReplaced (Diogo, 18/10/2026): trocar a CM no Editar
/// do Job On grava um cm_id novo num PostgreSQL real; o anterior fica com a
/// CM antiga, ligado à produção, e voltar a ela retoma-o.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task SwappingTheCm_CreatesANewCmId_TheOldOneKeepsTheOldCm()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CR-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = marker + "-CM2", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM1", marker + "-MF", marker + "-BQ");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
            var oldCmId = await db.CmContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();

            var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM2", marker + "-MF", marker + "-BQ"));

            Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            var current = await db.CmContexts.SingleAsync(c => c.JobOnId == jobOnId);
            Assert.NotEqual(oldCmId, current.Id);
            Assert.Equal(marker + "-CM2", current.ToolId);
            var replaced = await db.CmContexts.SingleAsync(c => c.Id == oldCmId);
            Assert.Null(replaced.JobOnId);
            Assert.Equal(jobOnId, replaced.ReplacedJobOnId);
            Assert.Equal(marker + "-CM1", replaced.ToolId);

            // Voltar à CM1 retoma o cm_id dela: o índice único do JobOnId
            // aceita a troca na mesma escrita.
            var back = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM1", marker + "-MF", marker + "-BQ"));

            Assert.Equal(UpdateJobOnResult.Success, back.Result);
            db.ChangeTracker.Clear();
            Assert.Equal(oldCmId, await db.CmContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync());
            Assert.Equal(2, await db.CmContexts.CountAsync(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }
}

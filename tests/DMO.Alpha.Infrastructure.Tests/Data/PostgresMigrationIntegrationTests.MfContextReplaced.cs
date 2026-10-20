using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migration MfContextReplaced (Diogo, 19/10/2026): trocar o MF no Editar
/// do Job On grava um mf_id novo num PostgreSQL real; o anterior fica com o
/// MF antigo, ligado à produção, e voltar a ele retoma-o.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task SwappingTheMf_CreatesANewMfId_TheOldOneKeepsTheOldMf()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "MR-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = marker + "-MF1", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-MF2", Type = ToolType.Mf, Reference = "5810", Lot = "2" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF1", marker + "-BQ");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
            var oldMfId = await db.MfContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();

            var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF2", marker + "-BQ"));

            Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            var current = await db.MfContexts.SingleAsync(c => c.JobOnId == jobOnId);
            Assert.NotEqual(oldMfId, current.Id);
            Assert.Equal(marker + "-MF2", current.ToolId);
            var replaced = await db.MfContexts.SingleAsync(c => c.Id == oldMfId);
            Assert.Null(replaced.JobOnId);
            Assert.Equal(jobOnId, replaced.ReplacedJobOnId);
            Assert.NotNull(replaced.ReplacedAtUtc);
            Assert.Equal(marker + "-MF1", replaced.ToolId);

            // Voltar ao MF1 retoma o mf_id dele: o índice único do JobOnId
            // aceita a troca na mesma escrita.
            var back = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF1", marker + "-BQ"));

            Assert.Equal(UpdateJobOnResult.Success, back.Result);
            db.ChangeTracker.Clear();
            Assert.Equal(oldMfId, await db.MfContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync());
            Assert.Equal(2, await db.MfContexts.CountAsync(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }
}

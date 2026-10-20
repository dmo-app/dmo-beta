using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// As produções (CM, MF, BQ) e as mudanças de lote ligadas às ferramentas,
/// contra um PostgreSQL real: a base recusa um tool_id inventado e apagar
/// uma ferramenta usada numa produção acaba bem (a produção passa para a
/// linha REMOVED).
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static async Task<int> SeedProductionWithToolsAsync(
        DmoDbContext db, string marker, string cmToolId, string mfToolId, string bqToolId)
    {
        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = marker, Machine = machine };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = cmToolId });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = mfToolId });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = bqToolId });
        db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOn = jobOn,
            Dia = new DateOnly(2026, 10, 17),
            Slot = "CM",
            NewToolId = cmToolId,
            ToolReferenceFrozen = "5447",
            NewLotFrozen = "3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return jobOn.Id;
    }

    private static async Task DeleteProductionAsync(DmoDbContext db, string marker)
    {
        var jobOns = db.JobOns.Where(j => j.ProductionNumber == marker).Select(j => j.Id);
        var controlos = db.Controlos.Where(c => jobOns.Contains(c.JobOnId)).Select(c => c.Id);
        await db.PesoComparacoes.Where(c => controlos.Contains(c.ControloId)).ExecuteDeleteAsync();
        await db.Pesos.Where(p => p.ControloId != null && controlos.Contains(p.ControloId.Value)).ExecuteDeleteAsync();
        await db.Controlos.Where(c => jobOns.Contains(c.JobOnId)).ExecuteDeleteAsync();
        await db.JobOnLotNotes.Where(n => jobOns.Contains(n.JobOnId)).ExecuteDeleteAsync();
        await db.CmContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
        await db.MfContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
        await db.BqContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();

        await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOns\" WHERE \"ProductionNumber\" = {marker}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Machines\" WHERE \"Code\" = {marker}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
    }

    [SkippableFact]
    public async Task ToolFks_RefuseAToolIdThatDoesNotExist()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TF-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await db.SaveChangesAsync();
        var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ");
        try
        {
            Assert.True(await db.Tools.AnyAsync(t => t.ToolId == ToolRegistryConstants.RemovedToolId));

            var invented = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"UPDATE \"CmContexts\" SET \"ToolId\" = {marker + "-NOPE"} WHERE \"JobOnId\" = {jobOnId}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invented.SqlState);
            Assert.Equal("FK_CmContexts_Tools_ToolId", invented.ConstraintName);

            var inventedNote = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"UPDATE \"JobOnLotNotes\" SET \"NewToolId\" = {marker + "-NOPE"} WHERE \"JobOnId\" = {jobOnId}"));
            Assert.Equal("FK_JobOnLotNotes_Tools_NewToolId", inventedNote.ConstraintName);
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task ToolFks_DeleteToolUsedInAProduction_MovesTheProductionToRemoved()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TF-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await db.SaveChangesAsync();
        var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ");
        try
        {
            // A FK recusa apagar a ferramenta diretamente.
            var direct = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"DELETE FROM \"Tools\" WHERE \"ToolId\" = {marker + "-CM"}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, direct.SqlState);

            var outcome = await new DeleteToolHandler(db).HandleAsync(new DeleteTool(marker + "-CM"));

            Assert.Equal(DeleteToolResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            Assert.False(await db.Tools.AnyAsync(t => t.ToolId == marker + "-CM"));
            Assert.Equal(ToolRegistryConstants.RemovedToolId,
                (await db.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId)).ToolId);
            Assert.Equal(ToolRegistryConstants.RemovedToolId,
                (await db.JobOnLotNotes.AsNoTracking().SingleAsync(n => n.JobOnId == jobOnId)).NewToolId);
            Assert.Equal(marker + "-MF",
                (await db.MfContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId)).ToolId);
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task ToolFks_DeleteCmUsedInAPesoComparison_MovesTheComparisonToRemoved()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TF-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await db.SaveChangesAsync();
        var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ");
        try
        {
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var peso = new Peso
            {
                ControloId = controlo.Id,
                CmToolId = marker + "-CM",
                BqToolId = marker + "-BQ",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            // Comparação antiga, sem factos congelados: o delete congela-os.
            var comparacao = new PesoComparacao
            {
                ControloId = controlo.Id,
                PesoInicialId = peso.Id,
                CmToolId = marker + "-CM",
                WaterTemperatureC = 20,
                WaterDensityGramsPerCm3 = 0.99717m,
                GlassDensityGramsPerCm3 = 2.50m,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                CreatedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc)
            };
            db.PesoComparacoes.Add(comparacao);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var outcome = await new DeleteToolHandler(db).HandleAsync(new DeleteTool(marker + "-CM"));

            Assert.Equal(DeleteToolResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            Assert.False(await db.Tools.AnyAsync(t => t.ToolId == marker + "-CM"));
            var guardada = await db.PesoComparacoes.AsNoTracking().SingleAsync(c => c.Id == comparacao.Id);
            Assert.Equal(ToolRegistryConstants.RemovedToolId, guardada.CmToolId);
            Assert.Equal("5447", guardada.CmToolReferenceFrozen);
            Assert.Equal("3", guardada.CmToolLotFrozen);
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task CreateJobOn_TwoSubmitsAtTheSameTime_OneSucceeds_TheOtherIsATypedRefusal()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TF-" + Guid.NewGuid().ToString("N")[..8];
        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            // Duplo clique: vários pares de envios iguais ao mesmo tempo, cada
            // um com a sua ligação. Nenhum dá exceção; cada par grava uma vez.
            for (var round = 0; round < 5; round++)
            {
                var number = marker + "-" + round;
                await using var a = CreateContext();
                await using var b = CreateContext();
                var outcomes = await Task.WhenAll(
                    new CreateJobOnHandler(a).HandleAsync(new CreateJobOn(number, "5810c142", machine.Id, MfToolId: marker + "-MF")),
                    new CreateJobOnHandler(b).HandleAsync(new CreateJobOn(number, "5810C142", machine.Id, MfToolId: marker + "-MF")));

                Assert.Single(outcomes, o => o.Result == CreateJobOnResult.Success);
                Assert.Single(outcomes, o => o.Result == CreateJobOnResult.DuplicateProductionNumber);
            }

            db.ChangeTracker.Clear();
            Assert.All(await db.JobOns.AsNoTracking().Where(j => j.MachineId == machine.Id).ToListAsync(),
                j => Assert.Equal("5810C142", j.Reference));
        }
        finally
        {
            var jobOns = db.JobOns.Where(j => j.MachineId == machine.Id).Select(j => j.Id);
            await db.Controlos.Where(c => jobOns.Contains(c.JobOnId)).ExecuteDeleteAsync();
            await db.MfContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Machines\" WHERE \"Code\" = {marker}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
        }
    }
}

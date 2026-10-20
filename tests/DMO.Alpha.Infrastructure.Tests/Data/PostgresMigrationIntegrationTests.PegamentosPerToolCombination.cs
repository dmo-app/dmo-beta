using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migração PegamentosPerToolCombination (sweep de 19/10/2026, DB-3) num
/// PostgreSQL real: a base recusa a segunda folha da mesma combinação no
/// mesmo controlo; aceita outra combinação e as folhas sem controlo;
/// associar uns Pegamentos sem Job On sem MF a uma produção com uma folha
/// antiga sem MF continua a passar; e apagar as duas CMs de um controlo
/// também (as folhas passam a REMOVED).
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static Pegamentos FolhaDe(int? controloId, string? cmToolId, string? bqToolId, string? mfToolId) => new()
    {
        ControloId = controloId,
        CmToolId = cmToolId,
        BqToolId = bqToolId,
        MfToolId = mfToolId,
        ToleranceMm = 0.20m
    };

    private static async Task DeletePegamentosPerToolCombinationAsync(DmoDbContext db, string marker, int controloId)
    {
        db.ChangeTracker.Clear();
        var sheets = db.Pegamentos.Where(p => p.ControloId == controloId || p.CmToolId!.StartsWith(marker)).Select(p => p.Id);
        await db.PegamentosMeasurements.Where(m => sheets.Contains(m.PegamentosId)).ExecuteDeleteAsync();
        await db.Pegamentos.Where(p => p.ControloId == controloId || p.CmToolId!.StartsWith(marker)).ExecuteDeleteAsync();
        await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(marker)).ExecuteDeleteAsync();
        await DeleteProductionAsync(db, marker);
    }

    private static async Task<PostgresException> RefusedSheetAsync(DmoDbContext db, Pegamentos sheet)
    {
        db.Pegamentos.Add(sheet);
        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        return Assert.IsType<PostgresException>(refused.InnerException);
    }

    [SkippableFact]
    public async Task Pegamentos_IndiceFiltrado_SegundaFolhaDaMesmaCombinacao_Recusa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "GC-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, _, _) = await SeedPegamentosFolhasAsync(db, marker);
        string cm = marker + "-CM", bq1 = marker + "-BQ1", bq2 = marker + "-BQ2", mf = marker + "-MF";
        try
        {
            db.Pegamentos.Add(FolhaDe(controloId, cm, bq1, mf));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var repeated = await RefusedSheetAsync(db, FolhaDe(controloId, cm, bq1, mf));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, repeated.SqlState);
            Assert.Equal("IX_Pegamentos_ControloId_CmToolId_BqToolId_MfToolId", repeated.ConstraintName);

            // Outra combinação no mesmo controlo e as folhas sem controlo
            // continuam aceites.
            db.Pegamentos.AddRange(
                FolhaDe(controloId, cm, bq2, mf),
                FolhaDe(null, cm, bq1, mf),
                FolhaDe(null, cm, bq1, mf));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Pegamentos.AsNoTracking().CountAsync(p => p.ControloId == controloId));
        }
        finally
        {
            await DeletePegamentosPerToolCombinationAsync(db, marker, controloId);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_IndiceFiltrado_AssociarSemJobOnSemMf_AProducaoComFolhaAntigaSemMf_Passa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "GA-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, _, _) = await SeedPegamentosFolhasAsync(db, marker);
        string cm = marker + "-CM", bq1 = marker + "-BQ1";
        try
        {
            // A folha de antes de a produção ter MF, e uns Pegamentos sem Job
            // On medidos sem MF: a associação aceita-os, como até aqui.
            db.Pegamentos.Add(FolhaDe(controloId, cm, bq1, null));
            var semJobOn = FolhaDe(null, cm, bq1, null);
            db.Pegamentos.Add(semJobOn);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var outcome = await new AssociatePegamentosToControloHandler(db)
                .HandleAsync(new AssociatePegamentosToControlo(controloId, semJobOn.Id));

            Assert.Equal(AssociatePegamentosToControloResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Pegamentos.AsNoTracking().CountAsync(p => p.ControloId == controloId && p.MfToolId == null));
        }
        finally
        {
            await DeletePegamentosPerToolCombinationAsync(db, marker, controloId);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_IndiceFiltrado_ApagarAsDuasCmsDoControlo_Passa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "GD-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, _, _) = await SeedPegamentosFolhasAsync(db, marker);
        string cm1 = marker + "-CM", cm2 = marker + "-CM2", bq1 = marker + "-BQ1", mf = marker + "-MF";
        try
        {
            db.Tools.Add(new Tool { ToolId = cm2, Type = ToolType.Cm, Reference = "5448", Lot = "2" });
            db.Pegamentos.AddRange(FolhaDe(controloId, cm1, bq1, mf), FolhaDe(controloId, cm2, bq1, mf));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // As duas folhas ficam com a mesma combinação (REMOVED, BQ, MF):
            // o índice não conta as ferramentas apagadas.
            Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool(cm1))).Result);
            db.ChangeTracker.Clear();
            Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool(cm2))).Result);
            db.ChangeTracker.Clear();

            var cms = await db.Pegamentos.AsNoTracking().Where(p => p.ControloId == controloId).Select(p => p.CmToolId).ToListAsync();
            Assert.Equal(2, cms.Count);
            Assert.All(cms, c => Assert.Equal(ToolRegistryConstants.RemovedToolId, c));
        }
        finally
        {
            await DeletePegamentosPerToolCombinationAsync(db, marker, controloId);
        }
    }
}

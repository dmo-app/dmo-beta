using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migração ContextToolIdentity (sweep de 19/10/2026, DB-1) num
/// PostgreSQL real: trocar a CM, o MF e a BQ, voltar às anteriores e apagar
/// as ferramentas continuam a passar; mudar a ferramenta de um cm_id, mf_id
/// ou bq_id no lugar, um contexto sem produção ou com as duas, uma troca sem
/// data e um segundo contexto da mesma ferramenta na mesma produção são
/// recusados pela base.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static async Task<(int JobOnId, int MachineId)> SeedContextToolIdentityAsync(DmoDbContext db, string marker)
    {
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = marker + "-CM2", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = marker + "-MF1", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-MF2", Type = ToolType.Mf, Reference = "5810", Lot = "2" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T194", Lot = "2" });
        await db.SaveChangesAsync();
        var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM1", marker + "-MF1", marker + "-BQ1");
        var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
        return (jobOnId, machineId);
    }

    private static async Task SwapAllToolsAsync(DmoDbContext db, int jobOnId, int machineId, string marker, string lot)
    {
        var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
            jobOnId, machineId, $"{marker}-CM{lot}", $"{marker}-MF{lot}", $"{marker}-BQ{lot}"));
        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        db.ChangeTracker.Clear();
    }

    // Os três contextos têm a mesma forma: as mesmas escritas provam as
    // regras em cada um.
    private static Task<List<string>> ContextToolIdsAsync<T>(DbSet<T> contexts, int jobOnId)
        where T : class =>
        contexts.AsNoTracking()
            .Where(c => EF.Property<int?>(c, "JobOnId") == jobOnId || EF.Property<int?>(c, "ReplacedJobOnId") == jobOnId)
            .OrderBy(c => EF.Property<int>(c, "Id"))
            .Select(c => EF.Property<string>(c, "ToolId"))
            .ToListAsync();

    private static IQueryable<T> CurrentContext<T>(DbSet<T> contexts, int jobOnId)
        where T : class =>
        contexts.Where(c => EF.Property<int?>(c, "JobOnId") == jobOnId);

    private static async Task AssertToolFixedAsync<T>(DbSet<T> contexts, int jobOnId, string toolId, string otherToolId)
        where T : class
    {
        var inPlace = await Assert.ThrowsAsync<PostgresException>(() => CurrentContext(contexts, jobOnId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => EF.Property<string>(c, "ToolId"), otherToolId)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, inPlace.SqlState);
        Assert.Contains("trocar a ferramenta cria um contexto novo", inPlace.MessageText);
        Assert.Equal(new[] { toolId }, await ContextToolIdsAsync(contexts, jobOnId));
    }

    private static async Task AssertOneProductionPerContextAsync<T>(
        DmoDbContext db, DbSet<T> contexts, string table, int jobOnId, string toolId)
        where T : class, new()
    {
        // Tirar a produção sem a passar para ReplacedJobOnId deixava o
        // contexto (e o registo da BQ) inalcançável.
        var detached = await Assert.ThrowsAsync<PostgresException>(() => CurrentContext(contexts, jobOnId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => EF.Property<int?>(c, "JobOnId"), (int?)null)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, detached.SqlState);
        Assert.Equal($"CK_{table}_CurrentOrReplaced", detached.ConstraintName);

        var both = await Assert.ThrowsAsync<PostgresException>(() => CurrentContext(contexts, jobOnId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => EF.Property<int?>(c, "ReplacedJobOnId"), (int?)jobOnId)
                .SetProperty(c => EF.Property<DateTime?>(c, "ReplacedAtUtc"), (DateTime?)DateTime.UtcNow)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, both.SqlState);
        Assert.Equal($"CK_{table}_CurrentOrReplaced", both.ConstraintName);

        var withoutDate = await Assert.ThrowsAsync<PostgresException>(() => CurrentContext(contexts, jobOnId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => EF.Property<int?>(c, "JobOnId"), (int?)null)
                .SetProperty(c => EF.Property<int?>(c, "ReplacedJobOnId"), (int?)jobOnId)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, withoutDate.SqlState);
        Assert.Equal($"CK_{table}_ReplacedWithDate", withoutDate.ConstraintName);

        // Voltar a uma ferramenta tem de retomar o contexto dela, não criar
        // outro.
        var repeated = new T();
        contexts.Add(repeated);
        db.Entry(repeated).Property("ReplacedJobOnId").CurrentValue = jobOnId;
        db.Entry(repeated).Property("ReplacedAtUtc").CurrentValue = DateTime.UtcNow;
        db.Entry(repeated).Property("ToolId").CurrentValue = toolId;
        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var unique = Assert.IsType<PostgresException>(refused.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, unique.SqlState);
        Assert.Equal($"IX_{table}_Production_ToolId", unique.ConstraintName);

        Assert.Equal(new[] { toolId }, await ContextToolIdsAsync(contexts, jobOnId));
    }

    [SkippableFact]
    public async Task ContextToolIdentity_TrocarVoltarEApagarAsFerramentas_Passam()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CI-" + Guid.NewGuid().ToString("N")[..8];
        var (jobOnId, machineId) = await SeedContextToolIdentityAsync(db, marker);
        try
        {
            // Trocar as três ferramentas cria três contextos novos; voltar às
            // anteriores retoma os delas.
            await SwapAllToolsAsync(db, jobOnId, machineId, marker, "2");
            await SwapAllToolsAsync(db, jobOnId, machineId, marker, "1");
            Assert.Equal(new[] { marker + "-CM1", marker + "-CM2" }, await ContextToolIdsAsync(db.CmContexts, jobOnId));
            Assert.Equal(new[] { marker + "-MF1", marker + "-MF2" }, await ContextToolIdsAsync(db.MfContexts, jobOnId));
            Assert.Equal(new[] { marker + "-BQ1", marker + "-BQ2" }, await ContextToolIdsAsync(db.BqContexts, jobOnId));

            // Apagar as seis ferramentas passa os contextos para REMOVED: dois
            // na mesma produção não contam como repetidos.
            foreach (var toolId in new[] { "CM1", "CM2", "MF1", "MF2", "BQ1", "BQ2" })
            {
                var deleted = await new DeleteToolHandler(db).HandleAsync(new DeleteTool($"{marker}-{toolId}"));
                Assert.Equal(DeleteToolResult.Success, deleted.Result);
                db.ChangeTracker.Clear();
            }

            var removed = new[] { ToolRegistryConstants.RemovedToolId, ToolRegistryConstants.RemovedToolId };
            Assert.Equal(removed, await ContextToolIdsAsync(db.CmContexts, jobOnId));
            Assert.Equal(removed, await ContextToolIdsAsync(db.MfContexts, jobOnId));
            Assert.Equal(removed, await ContextToolIdsAsync(db.BqContexts, jobOnId));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task ContextToolIdentity_MudarAFerramentaNoLugar_ABaseRecusa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CL-" + Guid.NewGuid().ToString("N")[..8];
        var (jobOnId, _) = await SeedContextToolIdentityAsync(db, marker);
        try
        {
            // Um handler que mudasse o cm_id no lugar, como o MF fazia até à
            // beta.256: a produção esqueceria a CM que usou.
            var cm = await db.CmContexts.SingleAsync(c => c.JobOnId == jobOnId);
            cm.ToolId = marker + "-CM2";
            var refused = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(refused.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
            Assert.Contains("trocar a ferramenta cria um contexto novo", postgres.MessageText);
            db.ChangeTracker.Clear();

            await AssertToolFixedAsync(db.CmContexts, jobOnId, marker + "-CM1", marker + "-CM2");
            await AssertToolFixedAsync(db.MfContexts, jobOnId, marker + "-MF1", marker + "-MF2");
            await AssertToolFixedAsync(db.BqContexts, jobOnId, marker + "-BQ1", marker + "-BQ2");
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task ContextToolIdentity_ContextoSemUmaSoProducaoOuRepetido_ABaseRecusa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CE-" + Guid.NewGuid().ToString("N")[..8];
        var (jobOnId, _) = await SeedContextToolIdentityAsync(db, marker);
        try
        {
            await AssertOneProductionPerContextAsync(db, db.CmContexts, "CmContexts", jobOnId, marker + "-CM1");
            await AssertOneProductionPerContextAsync(db, db.MfContexts, "MfContexts", jobOnId, marker + "-MF1");
            await AssertOneProductionPerContextAsync(db, db.BqContexts, "BqContexts", jobOnId, marker + "-BQ1");
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }
}

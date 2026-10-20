using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A migration AddIntegrityRules contra um PostgreSQL real: as regras novas
/// recusam o que não pode existir (duas máquinas com o mesmo código, um
/// controlo aprovado e não aprovado), e as corridas que antes davam erro 500
/// ou dados contraditórios (duas aberturas, duas leituras, dois chefes ao
/// mesmo tempo) acabam bem. A corrida é provocada por um interceptor que,
/// imediatamente antes da gravação do pedido em teste, grava o pedido
/// «concorrente» por outra ligação.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static DmoDbContext CreateContextWith(IInterceptor interceptor)
    {
        var builder = new NpgsqlConnectionStringBuilder(Connection!) { Pooling = false };
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new DmoDbContext(options);
    }

    /// <summary>Corre o pedido «concorrente» uma vez, antes da primeira gravação.</summary>
    private sealed class BeforeFirstSave(Func<Task> concurrent) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await concurrent();
            }

            return result;
        }
    }

    private static async Task<(int MachineId, int JobOnId)> SeedJobOnAsync(DmoDbContext db, string marker)
    {
        var machineId = (await db.Database
            .SqlQuery<int>($"INSERT INTO \"Machines\" (\"Code\") VALUES ({marker}) RETURNING \"Id\" AS \"Value\"")
            .ToListAsync()).Single();
        var jobOnId = (await db.Database
            .SqlQuery<int>($"INSERT INTO \"JobOns\" (\"Reference\", \"ProductionNumber\", \"MachineId\") VALUES ({marker}, {marker}, {machineId}) RETURNING \"Id\" AS \"Value\"")
            .ToListAsync()).Single();
        return (machineId, jobOnId);
    }

    private static async Task DeleteJobOnAsync(DmoDbContext db, int machineId)
    {
        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM \"ControloDecisoes\" WHERE \"ControloId\" IN (SELECT \"Id\" FROM \"Controlos\" WHERE \"JobOnId\" IN (SELECT \"Id\" FROM \"JobOns\" WHERE \"MachineId\" = {machineId}))");
        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM \"Controlos\" WHERE \"JobOnId\" IN (SELECT \"Id\" FROM \"JobOns\" WHERE \"MachineId\" = {machineId})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOns\" WHERE \"MachineId\" = {machineId}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Machines\" WHERE \"Id\" = {machineId}");
    }

    [SkippableFact]
    public async Task IntegrityRules_RefuseDuplicateMachineCode_AndContradictoryApproval()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "IR-" + Guid.NewGuid().ToString("N")[..8];
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Machines\" (\"Code\") VALUES ({marker})"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
            Assert.Equal("IX_Machines_Code", duplicate.ConstraintName);

            var both = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
                INSERT INTO "Controlos" ("JobOnId", "ApprovedByOperatorId", "ApprovedAtUtc", "NotApprovedByOperatorId", "NotApprovedAtUtc")
                VALUES ({jobOnId}, '1001', now(), '2002', now())
                """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, both.SqlState);
            Assert.Equal("CK_Controlos_ApprovedOrNotApproved", both.ConstraintName);
        }
        finally
        {
            await DeleteJobOnAsync(db, machineId);
        }
    }

    [SkippableFact]
    public async Task EnsureControlo_TwoOpeningsAtTheSameTime_BothGetTheSameControlo()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(setup, "IR-" + Guid.NewGuid().ToString("N")[..8]);
        var winnerId = 0;
        try
        {
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                winnerId = (await other.Database
                    .SqlQuery<int>($"INSERT INTO \"Controlos\" (\"JobOnId\") VALUES ({jobOnId}) RETURNING \"Id\" AS \"Value\"")
                    .ToListAsync()).Single();
            }));

            var outcome = await new EnsureControloForJobOnHandler(db).HandleAsync(new EnsureControloForJobOn(jobOnId));

            Assert.Equal(EnsureControloResult.Success, outcome.Result);
            Assert.NotEqual(0, winnerId);
            Assert.Equal(winnerId, outcome.ControloId);
            Assert.Equal(1, await setup.Controlos.CountAsync(c => c.JobOnId == jobOnId));
        }
        finally
        {
            await DeleteJobOnAsync(setup, machineId);
        }
    }

    [SkippableFact]
    public async Task Approval_ApproveAndNotApproveAtTheSameTime_LastDecisionWinsWhole()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(setup, "IR-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var controloId = (await setup.Database
                .SqlQuery<int>($"INSERT INTO \"Controlos\" (\"JobOnId\") VALUES ({jobOnId}) RETURNING \"Id\" AS \"Value\"")
                .ToListAsync()).Single();

            // O «Não aprovar» de outro chefe entra entre a leitura e a
            // gravação do «Aprovar». Antes, o «Aprovar» só gravava as suas
            // colunas e o controlo ficava com as duas decisões.
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await new ControloApprovalHandler(other).NotApproveAndGetAsync(controloId, "2002", "Mudar a CM.");
            }));

            await new ControloApprovalHandler(db).ApproveAndGetAsync(controloId, "1001", "Ok.");

            var final = await setup.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Equal("1001", final.ApprovedByOperatorId);
            Assert.NotNull(final.ApprovedAtUtc);
            Assert.Null(final.NotApprovedByOperatorId);
            Assert.Null(final.NotApprovedAtUtc);
            Assert.Equal("Ok.", final.ReviewNote);
        }
        finally
        {
            await DeleteJobOnAsync(setup, machineId);
        }
    }

    [SkippableFact]
    public async Task PesoMeasurement_TwoReadingsAtTheSameTime_GetDifferentPositions()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marker = "IR-" + Guid.NewGuid().ToString("N")[..8];
        setup.Tools.Add(new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        setup.Tools.Add(new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        var peso = new Peso
        {
            CmToolId = marker + "-CM",
            BqToolId = marker + "-BQ",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m
        };
        setup.Pesos.Add(peso);
        await setup.SaveChangesAsync();
        try
        {
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "PesoMeasurements" ("PesoId", "RowPosition", "CmSubject", "WaterWeightGrams")
                    VALUES ({peso.Id}, 1, '1', 199.50)
                    """);
            }));

            var outcome = await new RecordPesoMeasurementHandler(db)
                .HandleAsync(new RecordPesoMeasurement(peso.Id, "2", 199.64m));

            Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
            Assert.Equal(2, outcome.Measurement!.RowPosition);
            var positions = await setup.PesoMeasurements.AsNoTracking()
                .Where(m => m.PesoId == peso.Id)
                .OrderBy(m => m.RowPosition)
                .Select(m => m.RowPosition)
                .ToListAsync();
            Assert.Equal([1, 2], positions);
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Pesos\" WHERE \"Id\" = {peso.Id}");
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
        }
    }

    [SkippableFact]
    public async Task PegamentosMeasurement_DeleteRenumbers_UnderTheUniquePosition()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var registo = new Pegamentos
        {
            ToleranceMm = 0.20m,
            Measurements =
            [
                new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30.1m },
                new PegamentosMeasurement { RowPosition = 2, Component = PegamentosComponent.Cm, CosturaMm = 30.2m },
                new PegamentosMeasurement { RowPosition = 3, Component = PegamentosComponent.Cm, CosturaMm = 30.3m }
            ]
        };
        db.Pegamentos.Add(registo);
        await db.SaveChangesAsync();
        var first = registo.Measurements.Single(m => m.RowPosition == 1).Id;
        db.ChangeTracker.Clear();
        try
        {
            var result = await new CorrectPegamentosMeasurementHandler(db)
                .DeleteAsync(new PegamentosMeasurementAnchor(first, null, registo.Id));

            Assert.Equal(CorrectPegamentosMeasurementResult.Success, result);
            var rows = await db.PegamentosMeasurements.AsNoTracking()
                .Where(m => m.PegamentosId == registo.Id)
                .OrderBy(m => m.RowPosition)
                .Select(m => new { m.RowPosition, m.CosturaMm })
                .ToListAsync();
            Assert.Equal([1, 2], rows.Select(r => r.RowPosition));
            Assert.Equal([30.2m, 30.3m], rows.Select(r => r.CosturaMm));
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Pegamentos\" WHERE \"Id\" = {registo.Id}");
        }
    }
}

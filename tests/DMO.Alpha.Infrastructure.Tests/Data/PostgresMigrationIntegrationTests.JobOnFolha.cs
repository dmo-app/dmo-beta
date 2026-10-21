using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A folha da fábrica do Job On na base (migração AddJobOnFolha): as duas
/// tabelas novas existem com as colunas text do catálogo; o Criar e o Editar
/// gravam a folha e a CAL na mesma escrita do Job On, e uma recusa de tamanho
/// não deixa nada a meio; a folha apaga-se em cascata com o Job On.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task JobOnFolha_GravarELerDeNovo_EApagaEmCascataComOJobOn()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "FOL-" + Guid.NewGuid().ToString("N")[..8];
        var mf = marker + "-MF";
        db.Tools.Add(new Tool { ToolId = mf, Type = ToolType.Mf, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        var machineId = (await db.Database
            .SqlQuery<int>($"INSERT INTO \"Machines\" (\"Code\") VALUES ({marker}) RETURNING \"Id\" AS \"Value\"")
            .ToListAsync()).Single();
        try
        {
            // Uma coluna text por chave do catálogo, e a QTD Máq. na CAL.
            var colunas = await db.Database.SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'JobOnFolhas' AND data_type = 'text'
                """).ToListAsync();
            Assert.Equal(JobOnFolhaCampos.Todos.Select(c => c.Propriedade).Order(), colunas.Order());
            Assert.Equal(1, (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM information_schema.columns
                WHERE table_name = 'JobOnFolhaCalLinhas' AND column_name = 'QtdMaq' AND data_type = 'text'
                """).ToListAsync()).Single());

            int jobOnId;
            await using (var criar = CreateContext())
            {
                var outcome = await new CreateJobOnHandler(criar).HandleAsync(new CreateJobOn(
                    marker, marker, machineId, MfToolId: mf,
                    Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "12", ["notas_gerais"] = "a\r\nb" },
                    Cal: [new LinhaCalEnvio("Nível", "37.3", "2"), new LinhaCalEnvio("Tampão", null, null, "Tampão")]));
                Assert.Equal(CreateJobOnResult.Success, outcome.Result);
                jobOnId = outcome.JobOn!.JobOnId;
            }

            uint versao;
            await using (var leitura = CreateContext())
            {
                var lida = await new JobOnFolhaQuery(leitura).ForJobOnAsync(jobOnId);
                Assert.Equal(("12", "a\nb"), (lida.Valor("cm_qtd_stock"), lida.Valor("notas_gerais")));
                Assert.Equal(new[] { new JobOnFolhaCalLida(1, "Nível", "37.3", "2") }, lida.Cal);
                versao = (await new ProductionContextQuery(leitura).GetAsync(jobOnId))!.Version;
            }

            // Uma recusa de tamanho no Editar não grava nada, nem o resto da edição.
            await using (var recusa = CreateContext())
            {
                var outcome = await new UpdateJobOnHandler(recusa).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf, FimEm: new DateOnly(2026, 12, 1), ExpectedVersion: versao,
                    Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "13", ["cs_notas"] = new string('x', 1001) }));
                Assert.Equal(UpdateJobOnResult.TextoLongo, outcome.Result);
            }

            // Mudar só a folha grava, regista uma linha e muda a versão do Job On.
            await using (var editar = CreateContext())
            {
                var outcome = await new UpdateJobOnHandler(editar).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf, ExpectedVersion: versao,
                    Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "13", ["notas_gerais"] = "a\nb" },
                    Cal: [new LinhaCalEnvio("Nível", "37.3", "3")]));
                Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            }

            await using (var depois = CreateContext())
            {
                var lida = await new JobOnFolhaQuery(depois).ForJobOnAsync(jobOnId);
                Assert.Equal("13", lida.Valor("cm_qtd_stock"));
                Assert.Equal(new[] { new JobOnFolhaCalLida(1, "Nível", "37.3", "3") }, lida.Cal);
                var jobOn = await depois.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
                Assert.Null(jobOn.FimEm);
                var mudou = await depois.JobOnChanges.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
                Assert.Equal((JobOnChangeFields.Folha, "CM · QTD Stock, CAL"), (mudou.Field, mudou.NewValueFrozen));
                Assert.NotEqual(versao, (await new ProductionContextQuery(depois).GetAsync(jobOnId))!.Version);

                // Quem abriu antes e grava outra folha é recusado.
                var velha = await new UpdateJobOnHandler(depois).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf, ExpectedVersion: versao,
                    Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "99" }));
                Assert.Equal(UpdateJobOnResult.ChangedByAnotherUser, velha.Result);
            }

            // A base apaga a folha e a CAL com o Job On (ON DELETE CASCADE).
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" = {jobOnId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"JobOnId\" = {jobOnId}");
            await DeleteJobOnAsync(db, machineId);
            Assert.False(await db.JobOnFolhas.AnyAsync(f => f.JobOnId == jobOnId));
            Assert.False(await db.JobOnFolhaCalLinhas.AnyAsync(l => l.JobOnId == jobOnId));
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" IN (SELECT \"Id\" FROM \"JobOns\" WHERE \"MachineId\" = {machineId})");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"ToolId\" = {mf}");
            await DeleteJobOnAsync(db, machineId);
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {mf}");
        }
    }

    [SkippableFact]
    public async Task JobOnFolha_UmaFolhaPorJobOn_ABaseRecusaASegunda()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "FO2-" + Guid.NewGuid().ToString("N")[..8];
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"JobOnFolhas\" (\"JobOnId\", \"CmQtdStock\") VALUES ({jobOnId}, '1')");
            var segunda = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"JobOnFolhas\" (\"JobOnId\", \"CmQtdStock\") VALUES ({jobOnId}, '2')"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, segunda.SqlState);

            // E uma folha sem Job On não entra.
            var semJobOn = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"JobOnFolhaCalLinhas\" (\"JobOnId\", \"Ordem\") VALUES ({-jobOnId}, 1)"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, semJobOn.SqlState);
        }
        finally
        {
            await DeleteJobOnAsync(db, machineId);
        }
    }
}

using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Edições ao mesmo tempo (varredura de 18/10/2026, B4), no PostgreSQL: a
/// versão é o xmin da linha. Quem grava com a versão que abriu grava; quem
/// grava depois com a mesma versão (outra pessoa já gravou) é recusado e o
/// que a primeira pessoa gravou fica.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task EdicoesAoMesmoTempo_JobOn_ASegundaGravacaoComAVersaoVelhaERecusada()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "B4J-" + Guid.NewGuid().ToString("N")[..8];
        var mf1 = marker + "-MF1";
        var mf2 = marker + "-MF2";
        db.Tools.AddRange(
            new Tool { ToolId = mf1, Type = ToolType.Mf, Reference = marker, Lot = "1" },
            new Tool { ToolId = mf2, Type = ToolType.Mf, Reference = marker, Lot = "2" });
        await db.SaveChangesAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"MfContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOnId}, {mf1})");
            var aberta = (await new ProductionContextQuery(db).GetAsync(jobOnId))!.Version;
            Assert.NotEqual(0u, aberta);

            // A primeira pessoa grava com a versão que abriu.
            await using (var primeira = CreateContext())
            {
                var outcome = await new UpdateJobOnHandler(primeira).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf1, FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: aberta));
                Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            }

            // A segunda abriu antes e grava depois: recusada, fica o da primeira.
            await using (var segunda = CreateContext())
            {
                var outcome = await new UpdateJobOnHandler(segunda).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf1, FimEm: new DateOnly(2026, 4, 1), ExpectedVersion: aberta));
                Assert.Equal(UpdateJobOnResult.ChangedByAnotherUser, outcome.Result);
            }

            uint antesDaTroca;
            await using (var leitura = CreateContext())
            {
                var gravada = (await new ProductionContextQuery(leitura).GetAsync(jobOnId))!;
                Assert.Equal(new DateOnly(2026, 3, 20), gravada.FimEm);
                antesDaTroca = gravada.Version;

                // Mudar só o MF também muda a versão do Job On.
                var outcome = await new UpdateJobOnHandler(leitura).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf2, FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: antesDaTroca));
                Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            }

            await using (var depois = CreateContext())
            {
                var gravada = (await new ProductionContextQuery(depois).GetAsync(jobOnId))!;
                Assert.Equal(mf2, gravada.MfToolId);
                Assert.NotEqual(antesDaTroca, gravada.Version);

                // Quem abriu a ficha antes da troca do MF e grava depois é recusado.
                var outcome = await new UpdateJobOnHandler(depois).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, MfToolId: mf1, FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: antesDaTroca));
                Assert.Equal(UpdateJobOnResult.ChangedByAnotherUser, outcome.Result);
            }

            await using (var depoisDaRecusa = CreateContext())
            {
                Assert.Equal(mf2, (await new ProductionContextQuery(depoisDaRecusa).GetAsync(jobOnId))!.MfToolId);
            }

            // Entre a leitura e a escrita: a base recusa a gravação velha.
            await using (var a = CreateContext())
            await using (var b = CreateContext())
            {
                var lidaA = await a.JobOns.SingleAsync(j => j.Id == jobOnId);
                var lidaB = await b.JobOns.SingleAsync(j => j.Id == jobOnId);
                lidaB.CaloteMm = 3m;
                await b.SaveChangesAsync();
                lidaA.CaloteMm = 5m;
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync());
            }
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" = {jobOnId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"JobOnId\" = {jobOnId} OR \"ReplacedJobOnId\" = {jobOnId}");
            await DeleteJobOnAsync(db, machineId);
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" IN ({mf1}, {mf2})");
        }
    }

    [SkippableFact]
    public async Task EdicoesAoMesmoTempo_JobOn_OMesmoEnvioDuasVezes_GravaUmaVezSemRecusa()
    {
        // Duplo clique em «Guardar alterações» (sweep do Codex de 19/10/2026,
        // J-1): os dois envios levam a versão aberta e os mesmos dados. Um
        // grava; o outro, depois ou ao mesmo tempo, abre o que ficou gravado,
        // sem «Outra pessoa gravou» e sem outro bq_id nem outra linha em «O
        // que mudou».
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "J1-" + Guid.NewGuid().ToString("N")[..8];
        var mf = marker + "-MF";
        var bq1 = marker + "-BQ1";
        var bq3 = marker + "-BQ3";
        db.Tools.AddRange(
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = marker, Lot = "1" },
            new Tool { ToolId = bq1, Type = ToolType.Bq, Reference = marker, Lot = "1" },
            new Tool { ToolId = bq3, Type = ToolType.Bq, Reference = marker, Lot = "3" });
        await db.SaveChangesAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"MfContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOnId}, {mf})");
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"BqContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOnId}, {bq3})");
            var aberta = (await new ProductionContextQuery(db).GetAsync(jobOnId))!.Version;
            var envio = new UpdateJobOn(
                jobOnId, machineId, MfToolId: mf, BqToolId: bq1, FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: aberta);

            // Um depois do outro: o segundo chega com a versão de antes.
            for (var pedido = 0; pedido < 2; pedido++)
            {
                await using var ctx = CreateContext();
                Assert.Equal(UpdateJobOnResult.Success, (await new UpdateJobOnHandler(ctx).HandleAsync(envio)).Result);
            }

            // Ao mesmo tempo: um grava, o outro espera e encontra o gravado.
            for (var round = 0; round < 5; round++)
            {
                uint versao;
                await using (var leitura = CreateContext())
                {
                    versao = (await new ProductionContextQuery(leitura).GetAsync(jobOnId))!.Version;
                }

                var outraVez = envio with { FimEm = new DateOnly(2026, 4, 1 + round), ExpectedVersion = versao };
                await using var a = CreateContext();
                await using var b = CreateContext();
                var outcomes = await Task.WhenAll(
                    new UpdateJobOnHandler(a).HandleAsync(outraVez),
                    new UpdateJobOnHandler(b).HandleAsync(outraVez));

                Assert.All(outcomes, o => Assert.Equal(UpdateJobOnResult.Success, o.Result));
            }

            await using (var depois = CreateContext())
            {
                Assert.Equal(new DateOnly(2026, 4, 5), (await depois.JobOns.SingleAsync(j => j.Id == jobOnId)).FimEm);
                Assert.Equal(2, await depois.BqContexts.CountAsync(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId));
                var linhas = await depois.JobOnChanges.Where(c => c.JobOnId == jobOnId).Select(c => c.Field).ToListAsync();
                Assert.Equal(1, linhas.Count(f => f == JobOnChangeFields.Bq));
                Assert.Equal(6, linhas.Count(f => f == JobOnChangeFields.End));
            }
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" = {jobOnId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"JobOnId\" = {jobOnId} OR \"ReplacedJobOnId\" = {jobOnId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"BqContexts\" WHERE \"JobOnId\" = {jobOnId} OR \"ReplacedJobOnId\" = {jobOnId}");
            await DeleteJobOnAsync(db, machineId);
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" IN ({mf}, {bq1}, {bq3})");
        }
    }

    [SkippableFact]
    public async Task EdicoesAoMesmoTempo_Ferramenta_FactosETecnicaComAVersaoVelhaSaoRecusados()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "B4T-" + Guid.NewGuid().ToString("N")[..8];
        var toolId = marker + "-CM";
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        var machineId = (await db.Database
            .SqlQuery<int>($"INSERT INTO \"Machines\" (\"Code\") VALUES ({marker}) RETURNING \"Id\" AS \"Value\"")
            .ToListAsync()).Single();
        try
        {
            var aberta = (await new ToolSheetQuery(db).FindByToolIdAsync(toolId))!;
            Assert.NotEqual(0u, aberta.Version);
            Assert.Equal(0u, aberta.TechnicalVersion);

            await using (var primeira = CreateContext())
            {
                Assert.Equal(EditToolFactsResult.Success, (await new EditToolFactsHandler(primeira).HandleAsync(
                    new EditToolFacts(toolId, "CM", marker, "2", ExpectedVersion: aberta.Version))).Result);
                Assert.Equal(EditToolTechnicalValuesResult.Success, (await new EditToolTechnicalValuesHandler(primeira).HandleAsync(
                    new EditToolTechnicalValues(toolId, NominalWeightGrams: 250m, ExpectedVersion: aberta.TechnicalVersion))).Result);
            }

            await using (var segunda = CreateContext())
            {
                Assert.Equal(EditToolFactsResult.ChangedByAnotherUser, (await new EditToolFactsHandler(segunda).HandleAsync(
                    new EditToolFacts(toolId, "CM", marker, "3", ExpectedVersion: aberta.Version))).Result);
                Assert.Equal(EditToolTechnicalValuesResult.ChangedByAnotherUser, (await new EditToolTechnicalValuesHandler(segunda).HandleAsync(
                    new EditToolTechnicalValues(toolId, NominalWeightGrams: 300m, ExpectedVersion: aberta.TechnicalVersion))).Result);
            }

            await using (var leitura = CreateContext())
            {
                var gravada = (await new ToolSheetQuery(leitura).FindByToolIdAsync(toolId))!;
                Assert.Equal("2", gravada.Lot);
                Assert.Equal(250m, gravada.Technical!.NominalWeightGrams);
                Assert.NotEqual(0u, gravada.TechnicalVersion);

                // Mudar só as máquinas também muda a versão da Tool.
                Assert.Equal(EditToolFactsResult.Success, (await new EditToolFactsHandler(leitura).HandleAsync(
                    new EditToolFacts(toolId, "CM", marker, "2", MachineIds: [machineId], ExpectedVersion: gravada.Version))).Result);
                var depois = (await new ToolSheetQuery(leitura).FindByToolIdAsync(toolId))!;
                Assert.NotEqual(gravada.Version, depois.Version);
            }
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"ToolMachineLinks\" WHERE \"ToolId\" = {toolId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"ToolTechnicalContexts\" WHERE \"ToolId\" = {toolId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {toolId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Machines\" WHERE \"Id\" = {machineId}");
        }
    }
}

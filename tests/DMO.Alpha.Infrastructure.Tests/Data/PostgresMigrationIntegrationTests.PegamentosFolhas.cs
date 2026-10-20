using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Uma folha de Pegamentos por combinação de ferramentas (
/// 18/10/2026) num PostgreSQL real: a procura da folha compara as
/// ferramentas (também um MF em falta, «IS NULL» no SQL), o trinco do
/// controlo faz duas primeiras leituras depois de uma troca partilharem a
/// mesma folha nova, e uma produção só com a folha de antes da troca aceita
/// a associação da folha das ferramentas atuais.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static async Task<(int ControloId, int JobOnId, int MachineId)> SeedPegamentosFolhasAsync(
        DmoDbContext db, string marker, bool withMf = true)
    {
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T195", Lot = "2" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = marker + "-CM", DiametroGargaloMm = 70.00m },
            new ToolTechnicalContext { ToolId = marker + "-BQ1", DiametroGargaloMm = 30.00m },
            new ToolTechnicalContext { ToolId = marker + "-BQ2", DiametroGargaloMm = 30.50m },
            new ToolTechnicalContext { ToolId = marker + "-MF", DiametroGargaloMm = 60.00m });
        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = marker, Machine = machine };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = marker + "-CM" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = marker + "-BQ1" });
        if (withMf)
        {
            db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = marker + "-MF" });
        }

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (controlo.Id, jobOn.Id, machine.Id);
    }

    private static async Task DeletePegamentosFolhasAsync(DmoDbContext db, string marker)
    {
        var sheets = db.Pegamentos.Where(p => p.CmToolId == marker + "-CM").Select(p => p.Id);
        await db.PegamentosMeasurements.Where(m => sheets.Contains(m.PegamentosId)).ExecuteDeleteAsync();
        await db.Pegamentos.Where(p => p.CmToolId == marker + "-CM").ExecuteDeleteAsync();
        await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(marker)).ExecuteDeleteAsync();
        await DeleteProductionAsync(db, marker);
    }

    private static RecordPegamentosMeasurementHandler PegamentosHandler(DmoDbContext db) =>
        new(db, new PegamentosToleranceConfiguration(db));

    [SkippableFact]
    public async Task Pegamentos_DepoisDeTrocarABq_DuasPrimeirasLeiturasAoMesmoTempo_FicamNumaSoFolhaNova()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "PF-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, jobOnId, machineId) = await SeedPegamentosFolhasAsync(db, marker);
        try
        {
            var first = await PegamentosHandler(db).HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.05m));
            Assert.Equal(RecordPegamentosMeasurementResult.Success, first.Result);
            var swapped = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF", marker + "-BQ2"));
            Assert.Equal(UpdateJobOnResult.Success, swapped.Result);
            db.ChangeTracker.Clear();

            await using var a = CreateContext();
            await using var b = CreateContext();
            var outcomes = await Task.WhenAll(
                PegamentosHandler(a).HandleAsync(new RecordPegamentosMeasurement(controloId, "BQ", 30.55m)),
                PegamentosHandler(b).HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.10m)));

            Assert.All(outcomes, o => Assert.Equal(RecordPegamentosMeasurementResult.Success, o.Result));
            Assert.Equal(outcomes[0].Measurement!.PegamentosId, outcomes[1].Measurement!.PegamentosId);
            Assert.NotEqual(first.Measurement!.PegamentosId, outcomes[0].Measurement!.PegamentosId);
            Assert.Single(outcomes, o => o.Measurement!.StartedNewSheet);
            Assert.Equal(2, await db.Pegamentos.AsNoTracking().CountAsync(p => p.ControloId == controloId));

            // Voltar à BQ1 retoma a primeira folha.
            var back = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF", marker + "-BQ1"));
            Assert.Equal(UpdateJobOnResult.Success, back.Result);
            db.ChangeTracker.Clear();
            var again = await PegamentosHandler(db).HandleAsync(new RecordPegamentosMeasurement(controloId, "BQ", 30.05m));
            Assert.Equal(first.Measurement.PegamentosId, again.Measurement!.PegamentosId);
            Assert.Equal(2, again.Measurement.RowPosition);
        }
        finally
        {
            await DeletePegamentosFolhasAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_SemMf_AsLeiturasFicamNaMesmaFolha_EAFolhaMostradaEADelas()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "PN-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, _, _) = await SeedPegamentosFolhasAsync(db, marker, withMf: false);
        try
        {
            var first = await PegamentosHandler(db).HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.05m));
            db.ChangeTracker.Clear();
            var second = await PegamentosHandler(db).HandleAsync(new RecordPegamentosMeasurement(controloId, "BQ", 30.05m));

            Assert.Equal(first.Measurement!.PegamentosId, second.Measurement!.PegamentosId);
            Assert.False(second.Measurement.StartedNewSheet);
            Assert.Null((await db.Pegamentos.AsNoTracking().SingleAsync(p => p.ControloId == controloId)).MfToolId);

            var sheet = (await new PegamentosQuery(db, new ProductionContextQuery(db), new PegamentosToleranceConfiguration(db))
                .FindSheetByControloAsync(controloId))!;
            Assert.Equal(first.Measurement.PegamentosId, sheet.Pegamentos!.PegamentosId);
            Assert.True(Assert.Single(sheet.Sheets).IsCurrent);
        }
        finally
        {
            await DeletePegamentosFolhasAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_AssociarComSoAFolhaDeAntesDaTroca_FicaAFolhaDasFerramentasAtuais()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "PA-" + Guid.NewGuid().ToString("N")[..8];
        var (controloId, jobOnId, machineId) = await SeedPegamentosFolhasAsync(db, marker);
        try
        {
            var first = await PegamentosHandler(db).HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.05m));
            Assert.Equal(UpdateJobOnResult.Success, (await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, marker + "-CM", marker + "-MF", marker + "-BQ2"))).Result);
            db.ChangeTracker.Clear();
            var semJobOn = await new CreateToolScopedPegamentosHandler(db, new PegamentosToleranceConfiguration(db))
                .HandleAsync(new CreateToolScopedPegamentos(marker + "-CM", marker + "-BQ2", marker + "-MF"));

            var outcome = await new AssociatePegamentosToControloHandler(db).HandleAsync(
                new AssociatePegamentosToControlo(controloId, semJobOn.PegamentosId!.Value));

            Assert.Equal(AssociatePegamentosToControloResult.Success, outcome.Result);
            db.ChangeTracker.Clear();
            var sheets = await new PegamentosQuery(db, new ProductionContextQuery(db), new PegamentosToleranceConfiguration(db))
                .ListSheetsByControloAsync(controloId);
            Assert.Equal(new[] { first.Measurement!.PegamentosId, semJobOn.PegamentosId.Value }, sheets.Select(s => s.PegamentosId));
            Assert.Equal(new[] { false, true }, sheets.Select(s => s.IsCurrent));
        }
        finally
        {
            await DeletePegamentosFolhasAsync(db, marker);
        }
    }
}

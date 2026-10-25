using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tests.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O filtro da sobreposição em SQL (varredura de 18/10/2026, C2) dá no
/// PostgreSQL o mesmo aviso que a calculadora com todas as produções.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Overlap_FilteredInSql_NuncaAvisaEntreProducoesDaLinha()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var prefix = "OV" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            await JobOnOverlapScenario.AssertSameAsCalculatorAsync(db, prefix);
        }
        finally
        {
            await db.JobOns.Where(j => j.Reference == prefix + "REF").ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Code.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Overlap_NoDiaDaTroca_ENoDiaAntes_NuncaAvisam()
    {
        // Diogo, 21/10/2026: «uma producao sai dia 20 outra entra dia 20» não
        // é sobreposição. No PostgreSQL, a que entra no dia em que a outra
        // sai não avisa; a que entra no dia antes avisa.
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var prefix = "OT" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            var machine = new Machine { Code = prefix + "-M1" };
            db.Machines.Add(machine);
            var sai = new JobOn { Reference = prefix + "REF", ProductionNumber = "SAI", Machine = machine, InicioEm = new DateOnly(2026, 10, 10), FimEm = new DateOnly(2026, 10, 20) };
            var entra = new JobOn { Reference = prefix + "REF", ProductionNumber = "ENTRA", Machine = machine };
            db.JobOns.AddRange(sai, entra);
            await db.SaveChangesAsync();

            var noDia = await JobOnOverlapQuery.EvaluateAsync(db, entra.Id, machine.Id, machine.Code, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 30), CancellationToken.None);
            Assert.Null(noDia);

            var antes = await JobOnOverlapQuery.EvaluateAsync(db, entra.Id, machine.Id, machine.Code, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30), CancellationToken.None);
            // Sem aviso entre produções da linha (Diogo, 25/10/2026: só as BQ);
            // a calculadora continua a distinguir a véspera do dia da troca.
            Assert.Null(antes);
            Assert.False(JobOnOverlap.Crosses(sai.InicioEm, sai.FimEm, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 30)));
            Assert.True(JobOnOverlap.Crosses(sai.InicioEm, sai.FimEm, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30)));
        }
        finally
        {
            await db.JobOns.Where(j => j.Reference == prefix + "REF").ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Code.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }
}

using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Sweep de 19/10/2026, F-1: mudar o tipo de uma ferramenta já usada conta
/// as produções (atuais ou trocadas) do tipo anterior, traduzido no
/// PostgreSQL. O InMemory conta em C# e não apanha um <c>??</c> com
/// <c>Distinct</c> que o Npgsql não traduza; aí o Editar falhava em vez de
/// gravar. Os códigos têm algarismos ao acaso, para a base partilhada pelos
/// testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task TipoAnterior_ContaAsProducoesDaBq_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var toolId = $"TIPO-ANT-{p}";
        var machine = new Machine { Code = $"TA-{p}" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = $"T{p}", Lot = "1" });
        var j1 = new JobOn { Reference = $"5447T{p}", ProductionNumber = $"A{p}", Machine = machine };
        var j2 = new JobOn { Reference = $"5447T{p}", ProductionNumber = $"B{p}", Machine = machine };
        db.JobOns.AddRange(j1, j2);
        await db.SaveChangesAsync();

        try
        {
            // Numa produção está em uso; na outra foi trocada por outra BQ.
            db.BqContexts.AddRange(
                new BqContext { JobOnId = j1.Id, ToolId = toolId },
                new BqContext { ReplacedJobOnId = j2.Id, ReplacedAtUtc = DateTime.UtcNow, ToolId = toolId });
            await db.SaveChangesAsync();

            var outcome = await new EditToolFactsHandler(db).HandleAsync(new EditToolFacts(toolId, "CM", $"T{p}", "1"));
            Assert.Equal(EditToolFactsResult.Success, outcome.Result);
            Assert.Equal(ToolType.Bq, outcome.Tool!.PreviousType);
            Assert.Equal(2, outcome.Tool.PreviousTypeProductions);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.BqContexts.Where(c => c.ToolId == toolId).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}

using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The sheet reads moved out of JobOn/Index.cshtml.cs cost what they cost when
    // the page made them one by one: tool facts (only with tools and not for a
    // duplicate) and the saved factory sheet (2). The lot notes, the changes and
    // the delete preview are no longer read. They never write.
    [SkippableTheory]
    [InlineData("aberto", false, true, 10)]
    [InlineData("editar", false, true, 10)]
    [InlineData("duplicar", true, true, 2)]
    [InlineData("sem ferramentas", false, false, 2)]
    public async Task JobOnFicha_CadaPedidoFazSoAsConsultasDeLeitura(string caso, bool duplicar, bool comFerramentas, int consultas)
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "JF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker, InicioEm = new DateOnly(2026, 10, 1) };
        db.JobOns.Add(jobOn);
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-cm", Type = ToolType.Cm, Reference = marker, Lot = "1" },
            new Tool { ToolId = marker + "-mf", Type = ToolType.Mf, Reference = marker, Lot = "1" },
            new Tool { ToolId = marker + "-bq", Type = ToolType.Bq, Reference = marker, Lot = "1" });
        if (comFerramentas)
        {
            db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = marker + "-cm" });
            db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = marker + "-mf" });
            db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = marker + "-bq" });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var producao = (await new ProductionContextQuery(db).GetAsync(jobOn.Id))!;
        db.ChangeTracker.Clear();

        var query = new JobOnFichaQuery(
            new JobOnToolFactsQuery(db), new JobOnFolhaQuery(db));
        commands.Reading = true;
        var ficha = await query.FichaAsync(producao, duplicar);
        commands.Reading = false;

        Assert.True(consultas == commands.Reads, $"{caso}: {commands.Reads} consultas em vez de {consultas}");
        Assert.Equal(!duplicar && comFerramentas, ficha.ToolFacts.Count > 0);
        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The machine lookup moved out of JobOn/Reparacao.cshtml.cs keeps the
    // one-query cost it had in the page, and never writes.
    [SkippableFact]
    public async Task JobOnMaquina_FazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "JM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        var producao = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker };
        db.JobOns.Add(producao);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new JobOnPaginaQuery(db);
        commands.Reading = true;
        Assert.Equal(machine.Id, await query.MaquinaDoJobOnAsync(producao.Id));
        Assert.Equal(1, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
    }
}

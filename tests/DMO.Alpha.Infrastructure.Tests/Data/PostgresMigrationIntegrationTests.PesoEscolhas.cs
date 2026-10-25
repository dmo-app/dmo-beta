using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The page lookups moved out of Peso.cshtml.cs keep the one-query cost
    // they had when the page read the context itself.
    [SkippableFact]
    public async Task PesoEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "PE-" + Guid.NewGuid().ToString("N")[..8];
        var production = new JobOn { Machine = new Machine { Code = marker }, Reference = marker, ProductionNumber = marker };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = production };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new PesoEscolhasQuery(db);
        commands.Reading = true;
        Assert.Equal(controlo.Id, await query.ControloDaProducaoAsync(production.Id));
        Assert.Equal(1, commands.Reads);
        Assert.Equal(production.Id, await query.ProducaoDoControloAsync(controlo.Id));
        Assert.Equal(2, commands.Reads);
        Assert.Contains(marker, await query.CodigosDeMaquinasAsync());
        Assert.Equal(3, commands.Reads);
        Assert.False(await query.ComparacaoGuardadaExisteAsync(-1, -1));
        Assert.Equal(4, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

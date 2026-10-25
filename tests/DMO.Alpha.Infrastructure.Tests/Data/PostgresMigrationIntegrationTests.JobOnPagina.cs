using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The lookups moved out of JobOn/Index.cshtml.cs keep the one-query cost
    // they had when the page read the context itself, and never write.
    [SkippableFact]
    public async Task JobOnPagina_CadaPedidoFazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "JP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        var producao = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker, CaloteMm = 9m };
        db.JobOns.Add(producao);
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-mf", Type = ToolType.Mf, Reference = marker, Lot = "1" },
            new Tool { ToolId = marker + "-cm", Type = ToolType.Cm, Reference = marker, Lot = "2" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new JobOnPaginaQuery(db);
        commands.Reading = true;
        Assert.NotNull(await query.ProducaoDoHistoricoAsync(marker, marker, null, machine.Id));
        Assert.Equal(1, commands.Reads);
        Assert.Null(await query.AvisoDeSobreposicaoAsync(producao.Id, marker, null, null));
        Assert.Equal(2, commands.Reads);
        Assert.Equal(marker, await query.ReferenciaDaMfAsync(marker + "-mf"));
        Assert.Equal(3, commands.Reads);
        Assert.Equal(marker, await query.ReferenciaMfDaCmAsync(marker + "-cm"));
        Assert.Equal(4, commands.Reads);
        Assert.Equal(producao.Id, (await query.ProducaoComReferenciaENumeroAsync(marker, marker))?.Id);
        Assert.Equal(5, commands.Reads);
        Assert.Equal(9m, await query.CaloteGuardadaAsync(producao.Id));
        Assert.Equal(6, commands.Reads);
        Assert.Equal(marker, await query.CodigoDaMaquinaAsync(machine.Id));
        Assert.Equal(7, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

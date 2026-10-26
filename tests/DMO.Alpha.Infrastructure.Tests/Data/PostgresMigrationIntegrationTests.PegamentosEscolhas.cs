using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The page lookups moved out of Pegamentos.cshtml.cs keep the one-query
    // cost they had when the page read the context itself.
    [SkippableFact]
    public async Task PegamentosEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "PG-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        db.Machines.Add(new Machine { Code = marker });
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-9", Type = ToolType.Cm, Reference = marker, Lot = "9" },
            new Tool { ToolId = marker + "-10", Type = ToolType.Cm, Reference = marker, Lot = "10" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new PegamentosEscolhasQuery(db);
        commands.Reading = true;
        var lotes = await query.LotesDaReferenciaAsync(marker.ToLowerInvariant(), ToolType.Cm);
        Assert.Equal(new[] { "10", "9" }, lotes.Select(l => l.Lot));
        Assert.Equal(1, commands.Reads);
        Assert.Contains(marker, await query.CodigosDeMaquinasAsync());
        Assert.Equal(2, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

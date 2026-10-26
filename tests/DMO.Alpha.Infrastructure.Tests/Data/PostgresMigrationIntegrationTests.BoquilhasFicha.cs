using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // As leituras da ficha da BQ que saíram de Boquilhas/Index.cshtml.cs
    // mantêm o custo que tinham quando a página lia o contexto: uma só
    // consulta de leitura por pedido.
    [SkippableFact]
    public async Task BoquilhasFicha_CadaPedidoFazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "BF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = marker, Type = ToolType.Bq, Reference = marker, Lot = "7", BqAccountedQuantity = 60 });
        await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = marker, MachineId = machine.Id });
        var trace = new BqRepairTrace { ToolId = marker };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        db.BqMovements.Add(new BqMovement
        {
            BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 2,
            MachineCodeFrozen = marker, OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new BoquilhasFichaQuery(db);
        commands.Reading = true;
        Assert.Equal(new[] { marker }, (await query.MaquinasDaFerramentaAsync(marker)).Select(m => m.Code));
        Assert.Equal(1, commands.Reads);
        Assert.Equal(60, (await query.FerramentaAsync(marker))!.AccountedQuantity);
        Assert.Equal(2, commands.Reads);
        Assert.Single(await query.MovimentosSemJobOnAsync(marker, 20));
        Assert.Equal(3, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

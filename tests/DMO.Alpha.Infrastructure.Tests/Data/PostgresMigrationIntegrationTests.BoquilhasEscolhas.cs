using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // As leituras de escolha e de filtro que saíram de Boquilhas/Index.cshtml.cs
    // mantêm o custo que tinham quando a página lia o contexto: uma só
    // consulta de leitura por pedido (a lista das BQ vem com as máquinas e a
    // última linha na mesma consulta).
    [SkippableFact]
    public async Task BoquilhasEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "BE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker };
        db.JobOns.Add(jobOn);
        db.Tools.Add(new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = marker, Lot = "7" });
        await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = marker + "-BQ", MachineId = machine.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new BoquilhasEscolhasQuery(db);
        commands.Reading = true;
        var lidas = await query.BqSemJobOnAsync();
        Assert.Equal(new[] { marker }, lidas.Single(l => l.ToolId == marker + "-BQ").Machines);
        Assert.Equal(1, commands.Reads);
        Assert.Equal("7", (await query.FerramentaAsync(marker + "-BQ"))!.Lot);
        Assert.Equal(2, commands.Reads);
        Assert.Single(await query.BqsDaReferenciaAsync(marker));
        Assert.Equal(3, commands.Reads);
        Assert.Equal(marker, (await query.ProducaoDoFiltroAsync(jobOn.Id))!.Line);
        Assert.Equal(4, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

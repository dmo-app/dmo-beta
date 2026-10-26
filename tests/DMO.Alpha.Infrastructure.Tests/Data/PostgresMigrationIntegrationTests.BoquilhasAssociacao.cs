using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Os registos de uma BQ e a produção a associar, que saíram de
    // Boquilhas/Index.cshtml.cs, mantêm o custo que tinham quando a página
    // lia o contexto: a lista paginada faz 3 consultas (a contagem, a página e
    // os movimentos da página) e cada procura de produção faz uma só.
    [SkippableFact]
    public async Task BoquilhasAssociacao_CadaPedidoMantemOCustoEmConsultasDeLeitura()
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "BA-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker };
        db.Tools.Add(new Tool { ToolId = marker, Type = ToolType.Bq, Reference = marker, Lot = "7" });
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        var contexto = new BqContext { JobOnId = jobOn.Id, ToolId = marker };
        db.BqContexts.Add(contexto);
        var avulso = new BqRepairTrace { ToolId = marker };
        db.BqRepairTraces.Add(avulso);
        await db.SaveChangesAsync();
        db.BqMovements.Add(new BqMovement
        {
            BqRepairTraceId = avulso.Id, Type = BqMovementType.Saida, Quantity = 2, OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new BoquilhasAssociacaoQuery(db);
        commands.Reading = true;
        var pagina = await query.RegistosDaBqAsync(marker, 1);
        Assert.Equal(1, pagina.Items.Single().MovementCount);
        var depoisDaLista = commands.Reads;
        Assert.Equal(3, depoisDaLista);
        Assert.Equal(avulso.Id, (await query.RegistoSemJobOnAsync(marker))!.Id);
        Assert.Equal(depoisDaLista + 1, commands.Reads);
        Assert.Equal(marker, (await query.ProducaoAsync(jobOn.Id))!.Reference);
        Assert.Equal(depoisDaLista + 2, commands.Reads);
        Assert.True((await query.ProducoesDoNumeroAsync(marker, marker)).Single().TemBq);
        Assert.Equal(depoisDaLista + 3, commands.Reads);
        Assert.Equal(contexto.Id, await query.ContextoDaBqNaProducaoAsync(jobOn.Id, marker));
        Assert.Equal(depoisDaLista + 4, commands.Reads);
        commands.Reading = false;

        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

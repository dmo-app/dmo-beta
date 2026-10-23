using System.Data.Common;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task JobOnEditar_HistoricoDasFerramentas_NaoAumentaConsultasNemTrocaUltimoEstado()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var counter = new JobOnCommands();
        await using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(Connection!).AddInterceptors(counter).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var prefix = "JE" + Guid.NewGuid().ToString("N")[..10];
        var ids = Enumerable.Range(0, 3).Select(i => prefix + i).ToArray();
        foreach (var id in ids)
        {
            db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Cm, Reference = prefix, Lot = "1" });
            for (var n = 0; n < 100; n++)
            {
                db.FerramentaEstados.Add(new FerramentaEstado { ToolId = id, Estado = n == 99 ? FerramentaEstadoTipo.Reparado : FerramentaEstadoTipo.Novo, RegistadoEmUtc = DateTime.UtcNow });
                db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = id, Percentagem = n, RegistadaEmUtc = DateTime.UtcNow });
            }
        }
        await db.SaveChangesAsync();
        counter.Count = 0;
        var facts = await new JobOnToolFactsQuery(db).FindAsync(ids, "B1", null, DateOnly.FromDateTime(DateTime.UtcNow), null);
        Assert.Equal(3, facts.Count);
        Assert.All(facts, f => { Assert.Equal(99m, f.Utilizacao); Assert.Equal(FerramentaEstadoTipo.Reparado, f.Estado); });
        Assert.InRange(counter.Count, 1, 3);
    }

    private sealed class JobOnCommands : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}

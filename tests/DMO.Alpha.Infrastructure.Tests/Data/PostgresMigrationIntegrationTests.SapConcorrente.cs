using System.Data.Common;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    private sealed class AtrasarLeituraSap : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && (command.CommandText.Contains("\"SapFabricos\"", StringComparison.Ordinal)
                    || command.CommandText.Contains("\"FerramentaUtilizacoes\"", StringComparison.Ordinal)))
                await Task.Delay(150, cancellationToken);
            return result;
        }
    }

    private static DmoDbContext ContextoSapConcorrente() => new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseNpgsql(Connection!).AddInterceptors(new AtrasarLeituraSap()).Options);

    [SkippableFact]
    public async Task MesmoFicheiroSapEmDuasContas_GravaUmaSoLeitura()
    {
        SkipWithoutConnection();
        var marca = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var id = "SAP-" + marca;
        var tecnico = "M" + marca;
        await using (var seed = CreateContext())
        {
            seed.Tools.Add(new Tool { ToolId = id, Type = ToolType.Cm, Reference = marca, Lot = "7", TechnicalName = tecnico });
            await seed.SaveChangesAsync();
        }
        try
        {
            var html = $"<table><tr><th>Equipamento</th><th>% Use</th></tr><tr><td>{tecnico}07</td><td>20</td></tr></table>";
            await using var a = ContextoSapConcorrente();
            await using var b = ContextoSapConcorrente();
            await Task.WhenAll(new FerramentasUsoSap(a, new SessaoUsoSap()).ImportarAsync(html, "teste.html", null),
                new FerramentasUsoSap(b, new SessaoUsoSap()).ImportarAsync(html, "teste.html", null));
            await using var leitura = CreateContext();
            Assert.Single(await leitura.FerramentaUtilizacoes.AsNoTracking().Where(u => u.ToolId == id).ToListAsync());
        }
        finally
        {
            await using var clean = CreateContext();
            await clean.FerramentaUtilizacoes.Where(u => u.ToolId == id).ExecuteDeleteAsync();
            await clean.Tools.Where(t => t.ToolId == id).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task MesmoFabricoSapEmDuasContas_NaoColideNemDuplica()
    {
        SkipWithoutConnection();
        var material = "ZZ" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var linha = new FabricoSapLinha(material, "C3", new(2025, 1, 1), new(2025, 1, 2), 100, 90, 120m, 2);
        try
        {
            await using var a = ContextoSapConcorrente();
            await using var b = ContextoSapConcorrente();
            await Task.WhenAll(new FerramentasFabricoSap(a, new SessaoUsoSap()).ImportarAsync([linha], "teste.txt", null),
                new FerramentasFabricoSap(b, new SessaoUsoSap()).ImportarAsync([linha], "teste.txt", null));
            await using var leitura = CreateContext();
            Assert.Single(await leitura.SapFabricos.AsNoTracking().Where(s => s.Material == material).ToListAsync());
        }
        finally
        {
            await using var clean = CreateContext();
            await clean.SapFabricos.Where(s => s.Material == material).ExecuteDeleteAsync();
        }
    }
}

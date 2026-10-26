using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The history moved out of JobOn/Index.cshtml.cs keeps the reads it made when
    // the page composed it: the productions of the reference (1), the older
    // production asked by link (1, only when it is not in the dropdown) and the
    // page of results (2: count + rows, only when a filter is given).
    [SkippableTheory]
    [InlineData("vazio", 0)]
    [InlineData("referencia", 3)]
    [InlineData("referencia e producao do dropdown", 3)]
    [InlineData("referencia e producao antiga por link", 4)]
    [InlineData("referencia e producao inexistente", 4)]
    [InlineData("so datas", 2)]
    public async Task JobOnHistorico_CadaPedidoFazSoAsConsultasDeLeitura(string caso, int consultas)
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "JH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var machine = new Machine { Code = marker };
        for (var i = 1; i <= 8; i++)
        {
            db.JobOns.Add(new JobOn
            {
                Machine = machine, Reference = marker, ProductionNumber = "P" + i,
                InicioEm = new DateOnly(2026, 10, i), FimEm = new DateOnly(2026, 10, i + 1)
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new JobOnHistoricoQuery(new JobOnListQuery(db), new JobOnPaginaQuery(db));
        var temReferencia = caso.StartsWith("referencia", StringComparison.Ordinal);
        string? referencia = temReferencia ? marker : null;
        string? producao = caso switch
        {
            "referencia e producao do dropdown" => "P8",
            "referencia e producao antiga por link" => "P1",
            "referencia e producao inexistente" => "ZZ",
            _ => null
        };
        string? desde = caso == "so datas" ? "2026-10-01" : null;
        string? ate = caso == "so datas" ? "2026-10-05" : null;
        commands.Reading = true;
        var resultado = await query.HistoricoAsync(
            referencia, null, producao, desde, ate, null, [new MachineOption(machine.Id, marker)]);
        commands.Reading = false;

        Assert.Equal(consultas, commands.Reads);
        Assert.Equal(caso != "vazio", resultado.SearchApplied);
        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}

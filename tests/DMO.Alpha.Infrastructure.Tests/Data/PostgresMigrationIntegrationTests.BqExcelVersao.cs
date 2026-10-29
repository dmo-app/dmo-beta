using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// BQ-B05: a versão do Excel das Boquilhas tem de mudar quando se edita a
/// referência ou o lote de uma BQ, mesmo sem nenhum movimento novo — a
/// versão soma o RowVersion (o xmin real do PostgreSQL) das Tools BQ, que só
/// muda contra a base de dados real.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task VersaoAsync_MudaQuandoSeEditaOLoteDaBQ_SemNovoMovimento_BQ_B05()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var toolId = "BQ-EXCEL-" + Guid.NewGuid().ToString("N")[..10];
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "T173", Lot = "89" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        try
        {
            var excel = new BqExcelBoquilhas(db, new BqToolHistoryQuery(db));
            var versaoAntes = await excel.VersaoAsync();

            var outcome = await new EditToolFactsHandler(db).HandleAsync(new EditToolFacts(toolId, "BQ", "T173", "90"));
            Assert.Equal(EditToolFactsResult.Success, outcome.Result);
            db.ChangeTracker.Clear();

            var versaoDepois = await excel.VersaoAsync();

            Assert.NotEqual(versaoAntes, versaoDepois);
        }
        finally
        {
            await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
        }
    }
}

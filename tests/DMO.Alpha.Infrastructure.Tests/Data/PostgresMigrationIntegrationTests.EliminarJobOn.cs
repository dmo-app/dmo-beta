using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tests.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// «Eliminar Job On» no PostgreSQL (Diogo, 19/10/2026, D2): as chaves
/// estrangeiras RESTRICT (controlo, contextos trocados, Pesos, Pegamentos,
/// decisões) e os gatilhos dos contextos deixam apagar a produção inteira
/// numa só escrita, e a outra produção fica.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task EliminarJobOn_ApagaAProducaoInteira_ComAsChavesReais()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "EJ" + Guid.NewGuid().ToString("N")[..8];
        var ids = await EliminarJobOnCenario.SeedAsync(db, marker, DateOnly.FromDateTime(DateTime.UtcNow));

        await using (var outra = CreateContext())
        {
            var preview = await new DeleteJobOnHandler(outra).PreviewAsync(ids.JobOnId, DateOnly.FromDateTime(DateTime.UtcNow));
            Assert.Equal(2, preview!.BqMovements);
            Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(outra).HandleAsync(new DeleteJobOn(ids.JobOnId, "Diogo")));
        }

        await using var leitura = CreateContext();
        await EliminarJobOnCenario.AssertDeletedAsync(leitura, ids);
    }
}

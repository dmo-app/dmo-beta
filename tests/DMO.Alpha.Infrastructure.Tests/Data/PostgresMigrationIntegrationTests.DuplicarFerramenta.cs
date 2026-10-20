using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// «Duplicar para lote novo» ao mesmo tempo que outro Duplicar ou um Criar
/// do mesmo lote (deep sweep, 18/10/2026, I3-M-5): a janela envia um POST
/// nativo e, com a rede lenta, o duplo clique chega duas vezes ao servidor.
/// Fica uma só ferramenta com o mesmo tipo, referência e lote.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public Task Concurrency_DuplicateTheSameNewLotTwiceAtOnce_KeepsOneTool() =>
        DuplicateWhileAnotherWriteOfTheSameLotAsync(otherIsCreate: false);

    [SkippableFact]
    public Task Concurrency_DuplicateAndCreateTheSameLotAtOnce_KeepsOneTool() =>
        DuplicateWhileAnotherWriteOfTheSameLotAsync(otherIsCreate: true);

    private static async Task DuplicateWhileAnotherWriteOfTheSameLotAsync(bool otherIsCreate)
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var reference = "DF" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sourceId = "T" + Guid.NewGuid().ToString("N")[..10];
        db.Tools.Add(new Tool { ToolId = sourceId, Type = ToolType.Cm, Reference = reference, Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            // O outro pedido parte quando este já procurou o lote e vai gravar;
            // espera até acabar (sem a correção) ou até 1,5 s (com o trinco,
            // fica à espera de que este grave).
            Task<string>? other = null;
            await using var mine = CreateContextWith(new BeforeFirstSave(async () =>
            {
                other = Task.Run(async () =>
                {
                    await using var b = CreateContext();
                    if (otherIsCreate)
                    {
                        var created = await new CreateToolHandler(b, new ToolLotQuantitySettings(b)).HandleAsync(new CreateTool("CM", reference, "2"));
                        return created.Tool!.ToolId;
                    }
                    var duplicated = await new DuplicateToolHandler(b).HandleAsync(new DuplicateTool(sourceId, "2"));
                    return duplicated.Tool!.ToolId;
                });
                await Task.WhenAny(other, Task.Delay(1500));
            }));

            var outcome = await new DuplicateToolHandler(mine).HandleAsync(new DuplicateTool(sourceId, "2"));
            var otherToolId = await other!;

            Assert.Equal(DuplicateToolResult.Success, outcome.Result);
            Assert.Equal(outcome.Tool!.ToolId, otherToolId);
            Assert.Equal(1, await db.Tools.AsNoTracking().CountAsync(t => t.Reference == reference && t.Lot == "2"));
        }
        finally
        {
            await db.Tools.Where(t => t.Reference == reference).ExecuteDeleteAsync();
        }
    }
}

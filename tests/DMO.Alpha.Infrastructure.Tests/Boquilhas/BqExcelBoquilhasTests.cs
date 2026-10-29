using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// A versão do Excel das Boquilhas (<see cref="BqExcelBoquilhas.VersaoAsync"/>):
/// muda sempre que algo que o Excel mostra muda, mesmo sem um movimento novo
/// (BQ-B05 — editar a referência ou o lote de uma BQ não criava nenhum
/// movimento, por isso a versão ficava igual e o Excel ligado nunca era
/// reescrito).
/// </summary>
public sealed class BqExcelBoquilhasTests
{
    private static DmoDbContext CreateContext() => new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase("dmo-bq-excel-" + Guid.NewGuid().ToString("N"))
        .Options);

    // NOTA (BQ-B05): a versão soma o RowVersion ("xmin" no PostgreSQL real —
    // ver DmoDbContext.RowVersion) das Tools BQ; no InMemory este shadow
    // property nunca muda de valor sozinho (não há um xmin simulado), por
    // isso o caso de uma edição mudar a versão SEM nenhum movimento novo só
    // se prova contra o PostgreSQL real — ver
    // PostgresMigrationIntegrationTests.BqExcelVersao.cs
    // (VersaoAsync_MudaQuandoSeEditaOLoteDaBQ_SemNovoMovimento_BQ_B05).

    [Fact]
    public async Task VersaoAsync_IgualSemNenhumaMudanca()
    {
        using var db = CreateContext();
        db.Tools.Add(new Tool { ToolId = "bq-1", Type = ToolType.Bq, Reference = "T173", Lot = "89" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var excel = new BqExcelBoquilhas(db, new BqToolHistoryQuery(db));

        Assert.Equal(await excel.VersaoAsync(), await excel.VersaoAsync());
    }
}

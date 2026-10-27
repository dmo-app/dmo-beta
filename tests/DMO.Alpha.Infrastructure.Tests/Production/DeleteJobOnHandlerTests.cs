using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// «Eliminar Job On» (Diogo, 19/10/2026, D2 / AV-JO-04: «Uma forma de
/// verificar se já fez produção é comparar o dia atual com a data de início.
/// Se ultrapassar a data de início, ter um aviso que já produziu e os
/// problemas que pode causar ao eliminar»): a confirmação conta o que se
/// perde e avisa a partir do dia de início; eliminar apaga tudo o que é só
/// da produção, deixa rasto dos movimentos das Boquilhas e nada bloqueia.
/// O PostgreSQL prova o mesmo com as chaves estrangeiras reais
/// (PostgresMigrationIntegrationTests.EliminarJobOn).
/// </summary>
public sealed class DeleteJobOnHandlerTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);
    private readonly DmoDbContext _db;

    public DeleteJobOnHandlerTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Confirmacao_AvisaAPartirDoDiaDeInicio(int dias, bool avisa)
    {
        var ids = await EliminarJobOnCenario.SeedAsync(_db, "D1", Hoje.AddDays(dias));

        var preview = await new DeleteJobOnHandler(_db).PreviewAsync(ids.JobOnId, Hoje);

        Assert.Equal(avisa, preview!.StartReached);
    }

    [Fact]
    public async Task Confirmacao_SemDiaDeInicio_NaoAvisa()
    {
        var ids = await EliminarJobOnCenario.SeedAsync(_db, "D2", null);

        Assert.False((await new DeleteJobOnHandler(_db).PreviewAsync(ids.JobOnId, Hoje))!.StartReached);
    }

    [Fact]
    public async Task Confirmacao_ContaOQueSePerde()
    {
        var ids = await EliminarJobOnCenario.SeedAsync(_db, "D3", Hoje);

        var preview = await new DeleteJobOnHandler(_db).PreviewAsync(ids.JobOnId, Hoje);

        Assert.Equal(1, preview!.Pesos);
        Assert.Equal(1, preview.Pegamentos);
        Assert.Equal(1, preview.Puncoes);
        Assert.Equal(2, preview.BqMovements);
        Assert.True(preview.Revisto);
        Assert.Null(await new DeleteJobOnHandler(_db).PreviewAsync(ids.JobOnId + 999, Hoje));
    }

    [Fact]
    public async Task Eliminar_ApagaTudoDaProducao_DeixaRastoDosMovimentos_EAOutraProducaoFica()
    {
        var ids = await EliminarJobOnCenario.SeedAsync(_db, "D4", Hoje.AddDays(-5));

        var result = await new DeleteJobOnHandler(_db).HandleAsync(new DeleteJobOn(ids.JobOnId, "Diogo"));

        Assert.Equal(DeleteJobOnResult.Success, result);
        await EliminarJobOnCenario.AssertDeletedAsync(_db, ids);
    }

    [Fact]
    public async Task Eliminar_OutraVez_DizQueJaNaoExiste_SemMexerEmNada()
    {
        var ids = await EliminarJobOnCenario.SeedAsync(_db, "D5", Hoje);
        var handler = new DeleteJobOnHandler(_db);
        Assert.Equal(DeleteJobOnResult.Success, await handler.HandleAsync(new DeleteJobOn(ids.JobOnId)));

        Assert.Equal(DeleteJobOnResult.JobOnNotFound, await handler.HandleAsync(new DeleteJobOn(ids.JobOnId)));
        _db.ChangeTracker.Clear();
        Assert.True(await _db.JobOns.AnyAsync(j => j.Id == ids.OutraJobOnId));
    }
}

using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O ESTADO DO DOCUMENTO: OK/NOK e comentário por área (CM/BQ/MF/PU/CS),
/// uma linha por área; gravar de novo substitui; vocabulário fechado.
/// </summary>
public sealed class ControloAreaStateHandlerTests
{
    private static async Task<DmoDbContext> CreateContextWithControloAsync()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-area-states-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new DmoDbContext(options);
        var machine = new DMO.Alpha.Core.Production.Machine { Code = "B1" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var jobOn = new DMO.Alpha.Core.Production.JobOn { ProductionNumber = "202602", Reference = "1075", MachineId = machine.Id };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Get_WithoutRecords_ListsTheFiveAreasAsNotRegistered()
    {
        using var db = await CreateContextWithControloAsync();
        var states = await new ControloAreaStateHandler(db).GetAsync(db.Controlos.Single().Id);

        Assert.Equal(new[] { "CM", "BQ", "MF", "PU", "CS" }, states.Select(s => s.Area));
        Assert.All(states, s => { Assert.Null(s.Status); Assert.Null(s.Comment); });
    }

    [Fact]
    public async Task Set_SavesStatusAndComment_AndSecondSaveReplacesTheSameLine()
    {
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloAreaStateHandler(db);
        var controloId = db.Controlos.Single().Id;

        Assert.True(await handler.SetAsync(controloId, "bq", "nok", "  Boquilha marcada  ", "0099"));
        Assert.True(await handler.SetAsync(controloId, "CM", "OK", null, "0099"));
        Assert.True(await handler.SetAsync(controloId, "BQ", "OK", "Trocada", "0100"));

        var states = await handler.GetAsync(controloId);
        Assert.Equal(2, await db.ControloAreaStates.CountAsync());
        var bq = states.Single(s => s.Area == "BQ");
        Assert.Equal("OK", bq.Status);
        Assert.Equal("Trocada", bq.Comment);
        Assert.Equal("0100", bq.UpdatedBy);
        Assert.Equal("OK", states.Single(s => s.Area == "CM").Status);
    }

    [Fact]
    public async Task Set_CommentOnly_IsKept_AndEmptyStateRemovesTheLine()
    {
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloAreaStateHandler(db);
        var controloId = db.Controlos.Single().Id;

        Assert.True(await handler.SetAsync(controloId, "PU", null, "Por verificar", "0099"));
        Assert.Equal("Por verificar", (await handler.GetAsync(controloId)).Single(s => s.Area == "PU").Comment);

        Assert.True(await handler.SetAsync(controloId, "PU", "", " ", "0099"));
        Assert.Empty(await db.ControloAreaStates.ToListAsync());
    }

    [Fact]
    public async Task Set_OnlyOneField_KeepsTheOtherAsSaved()
    {
        // Deep sweep (18/10/2026): o OK/NOK e o comentário gravam-se em
        // separado; um posto com a folha antiga não apaga o que outro gravou.
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloAreaStateHandler(db);
        var controloId = db.Controlos.Single().Id;
        Assert.True(await handler.SetAsync(controloId, "CM", "NOK", "Rebarba no gargalo", "0100"));

        Assert.True(await handler.SetAsync(controloId, "CM", "OK", null, "0099", setComment: false));
        var cm = (await handler.GetAsync(controloId)).Single(s => s.Area == "CM");
        Assert.Equal(("OK", "Rebarba no gargalo"), (cm.Status, cm.Comment));

        Assert.True(await handler.SetAsync(controloId, "CM", null, "Trocada", "0100", setStatus: false));
        cm = (await handler.GetAsync(controloId)).Single(s => s.Area == "CM");
        Assert.Equal(("OK", "Trocada"), (cm.Status, cm.Comment));

        // «—» sem tocar no comentário tira só o estado.
        Assert.True(await handler.SetAsync(controloId, "CM", "", null, "0099", setComment: false));
        cm = (await handler.GetAsync(controloId)).Single(s => s.Area == "CM");
        Assert.Equal((null, "Trocada"), (cm.Status, cm.Comment));
    }

    [Theory]
    [InlineData("XX", "OK")]
    [InlineData("CM", "TALVEZ")]
    [InlineData(null, "OK")]
    public async Task Set_OutsideTheVocabulary_WritesNothing(string? area, string status)
    {
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloAreaStateHandler(db);

        Assert.False(await handler.SetAsync(db.Controlos.Single().Id, area, status, null, "0099"));
        Assert.False(await handler.SetAsync(9999, "CM", "OK", null, "0099"));
        Assert.Empty(await db.ControloAreaStates.ToListAsync());
    }
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O REGISTO DE VISÃO (o approve-mínimo, a decisão GATE 4 do Owner: «um
/// registo historico de como existia informação sobre acontecimentos e
/// foi visto pelo chefes... deve apenas deixar registado que ninguem
/// viu»): abrir a folha registra a visita (uma linha por operador;
/// reabrir atualiza o momento); NUNCA é gate — o handler só registra e
/// lista.
/// </summary>
public sealed class ControloViewingHandlerTests
{
    private static async Task<DmoDbContext> CreateContextWithControloAsync()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-viewings-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new DmoDbContext(options);

        // O jobon + o controlo de uma produção real (o ensure do Resumo).
        var machine = new DMO.Alpha.Core.Production.Machine { Code = "B1" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var jobOn = new DMO.Alpha.Core.Production.JobOn
        {
            ProductionNumber = "202602",
            Reference = "1075",
            MachineId = machine.Id
        };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id });
        await db.SaveChangesAsync();

        return db;
    }

    [Fact]
    public async Task Mark_FirstVisit_RegistersAndLists()
    {
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloViewingHandler(db);
        var controloId = db.Controlos.AsNoTracking().Single().Id;

        var viewings = await handler.MarkAndGetViewingsAsync(controloId, "0099");

        var viewing = Assert.Single(viewings);
        Assert.Equal("0099", viewing.OperatorId);
    }

    [Fact]
    public async Task Mark_Reopen_UpdatesTheSameLine_NeverDuplicates()
    {
        // O get-or-mark idempotente: uma linha por (controlo, operador) —
        // o índice único da tabela garante; reabrir a folha atualiza o
        // momento, não cria uma segunda visita.
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloViewingHandler(db);
        var controloId = db.Controlos.AsNoTracking().Single().Id;

        await handler.MarkAndGetViewingsAsync(controloId, "0099");
        await handler.MarkAndGetViewingsAsync(controloId, "0099");
        await handler.MarkAndGetViewingsAsync(controloId, "0099");

        var rows = await db.ControloViewings.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("0099", row.OperatorId);
    }

    [Fact]
    public async Task Mark_TwoOperators_TwoLines()
    {
        // O registo é POR OPERADOR: o chefe e o Owner têm as suas visitas.
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloViewingHandler(db);
        var controloId = db.Controlos.AsNoTracking().Single().Id;

        await handler.MarkAndGetViewingsAsync(controloId, "0099");
        var viewings = await handler.MarkAndGetViewingsAsync(controloId, "0001");

        Assert.Equal(2, viewings.Count);
    }

    [Fact]
    public async Task Mark_UnknownControlo_RegistersNothing()
    {
        // A folha inexistente não gera visitas — nenhum facto inventado.
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloViewingHandler(db);

        var viewings = await handler.MarkAndGetViewingsAsync(999999, "0099");

        Assert.Empty(viewings);
        Assert.Empty(await db.ControloViewings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Mark_EmptyOperator_RegistersNothing()
    {
        // Sem operador (sessão estranha) a visita não se inventa — só a
        // lista do que há.
        using var db = await CreateContextWithControloAsync();
        var handler = new ControloViewingHandler(db);
        var controloId = db.Controlos.AsNoTracking().Single().Id;

        var viewings = await handler.MarkAndGetViewingsAsync(controloId, null);

        Assert.Empty(viewings);
        Assert.Empty(await db.ControloViewings.AsNoTracking().ToListAsync());
    }
}

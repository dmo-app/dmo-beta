using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O histórico do OK/NOK (Diogo, 19/10/2026, D13: «se for do mesmo
/// [controlo], sim. Existir sempre forma de mudar, e a última mudança tem
/// efeito»): cada mudança fica com quem, quando e o valor; a última vale; os
/// outros controlos não entram.
/// </summary>
public sealed class ControloAreaStateHistoricoTests
{
    private static async Task<(DmoDbContext Db, int Primeiro, int Segundo)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-area-historico-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new DmoDbContext(options);
        var machine = new DMO.Alpha.Core.Production.Machine { Code = "B1" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var ids = new List<int>();
        foreach (var numero in new[] { "202601", "202602" })
        {
            var jobOn = new DMO.Alpha.Core.Production.JobOn { ProductionNumber = numero, Reference = "1075", MachineId = machine.Id };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            ids.Add(controlo.Id);
        }

        return (db, ids[0], ids[1]);
    }

    [Fact]
    public async Task CadaMudancaDoOkNok_FicaNoHistorico_EAUltimaVale()
    {
        var (db, controloId, outro) = await CreateAsync();
        using var _ = db;
        var handler = new ControloAreaStateHandler(db);

        Assert.True(await handler.SetAsync(controloId, "CM", "OK", null, "4801"));
        // A mesma escolha outra vez e o comentário não acrescentam nada.
        Assert.True(await handler.SetAsync(controloId, "CM", "OK", null, "4801"));
        Assert.True(await handler.SetAsync(controloId, "CM", null, "Visto", "4801", setStatus: false));
        Assert.True(await handler.SetAsync(controloId, "CM", "NOK", null, "4802", setComment: false));
        Assert.True(await handler.SetAsync(outro, "CM", "OK", null, "4803"));

        var cm = (await handler.GetAsync(controloId)).Single(a => a.Area == "CM");
        Assert.Equal("NOK", cm.Status);
        Assert.Equal("Visto", cm.Comment);
        Assert.Equal(new[] { "NOK", "OK" }, cm.Historico.Select(h => h.Status));
        Assert.Equal(new[] { "4802", "4801" }, cm.Historico.Select(h => h.Por));
        var anterior = Assert.Single(cm.Anteriores);
        Assert.Equal("OK", anterior.Status);
        Assert.Empty((await handler.GetAsync(controloId)).Single(a => a.Area == "BQ").Historico);
    }

    [Fact]
    public async Task VoltarATraco_FicaNoHistorico()
    {
        var (db, controloId, _) = await CreateAsync();
        using var __ = db;
        var handler = new ControloAreaStateHandler(db);

        Assert.True(await handler.SetAsync(controloId, "BQ", "NOK", null, "4801", setComment: false));
        Assert.True(await handler.SetAsync(controloId, "BQ", null, null, "4802", setComment: false));

        var bq = (await handler.GetAsync(controloId)).Single(a => a.Area == "BQ");
        Assert.Null(bq.Status);
        Assert.Equal(new string?[] { null, "NOK" }, bq.Historico.Select(h => h.Status));
    }

    [Fact]
    public async Task UmOkGravadoAntesDoHistorico_EntraPrimeiroAoMudar()
    {
        var (db, controloId, _) = await CreateAsync();
        using var __ = db;
        var antes = new DateTime(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);
        db.ControloAreaStates.Add(new DMO.Alpha.Core.Controlo.ControloAreaState
        {
            ControloId = controloId, Area = "MF", Status = "OK", UpdatedBy = "4700", UpdatedAtUtc = antes
        });
        await db.SaveChangesAsync();

        Assert.True(await new ControloAreaStateHandler(db).SetAsync(controloId, "MF", "NOK", null, "4801", setComment: false));

        var mf = (await new ControloAreaStateHandler(db).GetAsync(controloId)).Single(a => a.Area == "MF");
        Assert.Equal(new[] { "NOK", "OK" }, mf.Historico.Select(h => h.Status));
        Assert.Equal("4700", mf.Anteriores.Single().Por);
        Assert.Equal(antes, mf.Anteriores.Single().EmUtc);
    }
}

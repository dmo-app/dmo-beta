using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova o read path da consulta do Job On: a lista dos Job Ons
/// existentes com o contexto de produção estabelecido (a MESMA projeção
/// da query canónica por jobon_id), mais recentes primeiro, com os
/// contextos aplicáveis de cada produção — cada produção mantém o seu
/// próprio contexto.
/// </summary>
public sealed class JobOnListQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly JobOnListQuery _query;

    public JobOnListQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new JobOnListQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<JobOn> SeedJobOnAsync(string reference, string productionNumber, string machineCode)
    {
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            Machine = machine
        };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        return jobOn;
    }

    [Fact]
    public async Task SearchHistoryPageAsync_ReturnsJobOnsWithEstablishedContext()
    {
        var first = await SeedJobOnAsync("5810", "PN-1001", "B1");
        var second = await SeedJobOnAsync("5447", "PN-1002", "B2");

        var cm = new CmContext { JobOn = second, ToolId = "T1" };
        var bq = new BqContext { JobOn = second, ToolId = "T-BQ1" };
        _db.CmContexts.Add(cm);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();

        var result = (await _query.SearchHistoryPageAsync(new JobOnHistoryFilter(null, null, null, null, null), null)).Items;

        // Mais recentes primeiro (jobon_id decrescente) e o contexto de
        // cada produção tal como estabelecido.
        Assert.Equal(2, result.Count);
        var newest = result[0];
        Assert.Equal(second.Id, newest.JobOnId);
        Assert.Equal("PN-1002", newest.ProductionNumber);
        Assert.Equal("5447", newest.Reference);
        Assert.Equal("B2", newest.Machine);
        Assert.Equal(cm.Id, newest.CmId);
        Assert.Equal("T1", newest.CmToolId);
        Assert.Null(newest.MfId);
        Assert.Null(newest.MfToolId);
        Assert.Equal(bq.Id, newest.BqId);
        Assert.Equal("T-BQ1", newest.BqToolId);

        var oldest = result[1];
        Assert.Equal(first.Id, oldest.JobOnId);
        Assert.Equal("B1", oldest.Machine);
        Assert.Null(oldest.CmId);
        Assert.Null(oldest.MfId);
        Assert.Null(oldest.BqId);
    }

    [Fact]
    public async Task SearchHistoryPageAsync_EmptyProduction_ReturnsEmptyList()
    {
        var result = (await _query.SearchHistoryPageAsync(new JobOnHistoryFilter(null, null, null, null, null), null)).Items;

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListProductionsOfReferenceAsync_SoAsUltimas5()
    {
        // A regra do proprietário (17/10/2026): o dropdown das produções mostra só as últimas 5.
        for (var i = 1; i <= 8; i++)
        {
            await SeedJobOnAsync("6018Y232", $"P{i:00}", "C2");
        }

        var options = await _query.ListProductionsOfReferenceAsync("6018");

        Assert.Equal(new[] { "P08", "P07", "P06", "P05", "P04" }, options.Select(o => o.ProductionNumber));
    }

    // Deep sweep 2 (18/10/2026, F-17): os repetidos tirados depois do corte
    // deixavam menos de 5 opções e juntavam duas referências numa só. Cada
    // opção é uma produção, com a sua referência.
    [Fact]
    public async Task ListProductionsOfReferenceAsync_OMesmoNumeroEmDuasReferencias_CincoOpcoesComAReferencia()
    {
        await SeedJobOnAsync("5447T173", "2026001", "C2");
        await SeedJobOnAsync("5447Y232", "2026001", "C2");
        await SeedJobOnAsync("5447T173", "2026002", "C2");
        await SeedJobOnAsync("5447Y232", "2026002", "C2");
        await SeedJobOnAsync("5447T173", "2026003", "C2");
        await SeedJobOnAsync("5447Y232", "2026003", "C2");

        var options = await _query.ListProductionsOfReferenceAsync("5447");

        Assert.Equal(
            new[] { ("2026003", "5447Y232"), ("2026003", "5447T173"), ("2026002", "5447Y232"), ("2026002", "5447T173"), ("2026001", "5447Y232") },
            options.Select(o => (o.ProductionNumber, o.Reference)));
    }

    // A regra do Diogo (18/10/2026): o início da referência ou da parte BQ,
    // nunca um carácter a meio; sem distinguir maiúsculas.
    [Theory]
    [InlineData("35", new[] { "3540Y079" })]
    [InlineData("0", new[] { "0132T173" })]
    [InlineData("y2", new[] { "6018Y232" })]
    [InlineData("T17", new[] { "0132T173" })]
    [InlineData("018", new string[0])]
    public async Task ListProductionsOfReferenceAsync_EHistorico_PeloInicioDaReferenciaOuDaParteBq(string texto, string[] expected)
    {
        await SeedJobOnAsync("3540Y079", "P1", "C2");
        await SeedJobOnAsync("4996T350", "P2", "C2");
        await SeedJobOnAsync("2459T035", "P3", "C2");
        await SeedJobOnAsync("0132T173", "P4", "C2");
        await SeedJobOnAsync("1075C142", "P5", "C2");
        await SeedJobOnAsync("6018Y232", "P6", "C2");

        var options = await _query.ListProductionsOfReferenceAsync(texto);
        var history = await _query.SearchHistoryPageAsync(new JobOnHistoryFilter(texto, null, null, null, null), null);

        Assert.Equal(expected, options.Select(o => o.Reference));
        Assert.Equal(expected, history.Items.Select(j => j.Reference));
    }

    [Fact]
    public async Task SearchHistoryPageAsync_ComAReferenciaDaProducao_SoEssaProducao()
    {
        await SeedJobOnAsync("5447T173", "2026001", "C2");
        await SeedJobOnAsync("5447Y232", "2026001", "C2");

        var both = await _query.SearchHistoryPageAsync(new JobOnHistoryFilter("5447", null, "2026001", null, null), null);
        var one = await _query.SearchHistoryPageAsync(new JobOnHistoryFilter("5447", null, "2026001", null, null, "5447y232"), null);

        Assert.Equal(2, both.TotalCount);
        Assert.Equal("5447Y232", Assert.Single(one.Items).Reference);
    }

    // Deep sweep 2 (18/10/2026, F-7): «3540y079» escrito em minúsculas não
    // encontrava as produções da 3540Y079.
    [Theory]
    [InlineData("3540y079")]
    [InlineData(" 3540y079 ")]
    [InlineData("3540Y079")]
    public async Task ListByReferenceAsync_EListPageOfReferenceAsync_SemDistinguirMaiusculas(string texto)
    {
        await SeedJobOnAsync("3540Y079", "P1", "C2");
        await SeedJobOnAsync("3540Y079", "P2", "B1");
        await SeedJobOnAsync("3540Y0790", "OUTRA", "C2");

        Assert.Equal(new[] { "P2", "P1" }, (await _query.ListByReferenceAsync(texto, null)).Select(j => j.ProductionNumber));
        Assert.Equal(new[] { "P1" }, (await _query.ListByReferenceAsync(texto, "C2")).Select(j => j.ProductionNumber));
        Assert.Equal(2, (await _query.ListPageOfReferenceAsync(texto, null, 10)).TotalCount);
        // A referência inteira: o início não chega.
        Assert.Empty(await _query.ListByReferenceAsync("3540", null));
    }

    [Fact]
    public async Task ListByReferenceAsync_SoAsUltimas5()
    {
        for (var i = 1; i <= 7; i++)
        {
            await SeedJobOnAsync("6018Y232", $"P{i:00}", "C2");
        }

        var items = await _query.ListByReferenceAsync("6018Y232", null);

        Assert.Equal(IProductionPickerQuery.MaxResults, items.Count);
        Assert.Equal("P07", items[0].ProductionNumber);
    }

    [Fact]
    public async Task ListPageOfReferenceAsync_TodasAsDaReferenciaExata_Paginadas_ComAsFerramentas()
    {
        for (var i = 1; i <= 12; i++)
        {
            await SeedJobOnAsync("6018Y232", $"P{i:00}", "C2");
        }
        await SeedJobOnAsync("6018Y2320", "OUTRA", "C1");
        var comFerramentas = await SeedJobOnAsync("6018Y232", "P13", "B3");
        _db.CmContexts.Add(new CmContext { JobOn = comFerramentas, ToolId = "CM-1" });
        _db.MfContexts.Add(new MfContext { JobOn = comFerramentas, ToolId = "MF-1" });
        _db.BqContexts.Add(new BqContext { JobOn = comFerramentas, ToolId = "BQ-1" });
        await _db.SaveChangesAsync();

        var page1 = await _query.ListPageOfReferenceAsync(" 6018Y232 ", null, 10);
        var page2 = await _query.ListPageOfReferenceAsync("6018Y232", 2, 10);

        Assert.Equal(13, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal("P13", page1.Items[0].ProductionNumber);
        Assert.Equal(("CM-1", "MF-1", "BQ-1"), (page1.Items[0].CmToolId, page1.Items[0].MfToolId, page1.Items[0].BqToolId));
        Assert.Equal(new[] { "P03", "P02", "P01" }, page2.Items.Select(j => j.ProductionNumber));
        Assert.DoesNotContain(page1.Items.Concat(page2.Items), j => j.ProductionNumber == "OUTRA");
    }

    [Fact]
    public async Task ListNextPerMachineAsync_APrimeiraAEntrarDepoisDoDia_PorMaquina()
    {
        var dia = new DateOnly(2026, 10, 17);
        var b1 = new Machine { Code = "B1" };
        var c2 = new Machine { Code = "C2" };
        var c3 = new Machine { Code = "C3" };
        _db.JobOns.AddRange(
            new JobOn { Reference = "6018Y232", ProductionNumber = "202602", Machine = c2, InicioEm = dia.AddDays(-5) },
            new JobOn { Reference = "6430T194", ProductionNumber = "202630", Machine = c2, InicioEm = dia.AddDays(10) },
            new JobOn { Reference = "5447T173", ProductionNumber = "202616", Machine = c2, InicioEm = dia.AddDays(3) },
            new JobOn { Reference = "1199C194", ProductionNumber = "202620", Machine = b1, InicioEm = dia.AddDays(1) },
            new JobOn { Reference = "1199C194", ProductionNumber = "202621", Machine = b1, InicioEm = dia.AddDays(1) },
            new JobOn { Reference = "5804T200", ProductionNumber = "202609", Machine = c3, InicioEm = dia },
            new JobOn { Reference = "SEMDIA", ProductionNumber = "202699", Machine = c3 });
        await _db.SaveChangesAsync();

        var proximas = await _query.ListNextPerMachineAsync(dia);

        Assert.Equal(2, proximas.Count);
        var naC2 = Assert.Single(proximas, p => p.Machine == "C2");
        Assert.Equal(("5447T173", "202616", dia.AddDays(3)), (naC2.Reference, naC2.ProductionNumber, naC2.InicioEm!.Value));
        // Em empate fica a gravada primeiro; a C3 só tem a do próprio dia e uma sem dia.
        Assert.Equal("202620", Assert.Single(proximas, p => p.Machine == "B1").ProductionNumber);
    }
}

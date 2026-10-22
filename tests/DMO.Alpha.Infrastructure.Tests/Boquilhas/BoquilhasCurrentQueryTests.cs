using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// A PRODUÇÃO CORRENTE de cada máquina para as Boquilhas (o corte F + O
/// CORTE B — a decisão do Owner 2026-10-12: «o teste tem de ser feito
/// pelo jobon, ver se os cards das maquinas refletem o bq_id que foi
/// planeado para a data do jobon»): o card da máquina reflete o BQ_ID
/// PLANEADO NO JOB ON (o BqContext da produção corrente — a HERANÇA do
/// canal jobon), nunca uma escolha do módulo. COM O CORTE B: a corrente
/// é a produção cujas DATAS DO JOB ON cobrem o DIA OPERACIONAL (o
/// Owner: «em produçao é a que esta hj em maquina e segue as datas do
/// jobon»); o fallback de transição mantém os Job Ons sem janela (a
/// última sem datas). Máquina sem produção → «Sem produção», nunca a
/// mais recente futura.
/// </summary>
public sealed class BoquilhasCurrentQueryTests
{
    /// <summary>O stub das definições: null = o default 8h (a manhã da fábrica).</summary>
    private sealed class StubSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private static async Task<DmoDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-boquilhas-current-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new DmoDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    [Theory]
    [InlineData("2026-07-15T06:59:00Z", "BEFORE")]
    [InlineData("2026-07-15T07:00:00Z", "AFTER")]
    [InlineData("2026-01-15T07:59:00Z", "BEFORE")]
    [InlineData("2026-01-15T08:00:00Z", "AFTER")]
    public async Task CurrentProduction_ChangesAtEightInLisbonRatherThanServerTime(string instant, string expected)
    {
        using var db = await CreateContextAsync();
        var utc = DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);
        var day = DateOnly.FromDateTime(utc.UtcDateTime);
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.JobOns.AddRange(
            new JobOn { Machine = machine, Reference = "T173", ProductionNumber = "BEFORE", InicioEm = day.AddDays(-1), FimEm = day.AddDays(-1) },
            new JobOn { Machine = machine, Reference = "T194", ProductionNumber = "AFTER", InicioEm = day, FimEm = day });
        await db.SaveChangesAsync();
        var query = new BoquilhasCurrentQuery(db, new StubSettings(), new FixedClock(utc));
        Assert.Equal(expected, Assert.Single(await query.ListAsync()).ProductionNumber);
    }

    /// <summary>As definições com a hora de viragem gravada no Canal Jobon.</summary>
    private sealed class HourSettings(string hour) : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(settingKey == ModuleSettingKeys.BoquilhasDayChangeHour ? hour : null);
    }

    [Theory]
    [InlineData("2030-03-11T03:00:00Z", null, "2030-03-10")]
    [InlineData("2030-03-11T08:00:00Z", null, "2030-03-11")]
    [InlineData("2030-07-11T06:59:00Z", null, "2030-07-10")]
    [InlineData("2030-03-11T06:00:00Z", "6", "2030-03-11")]
    public async Task OperationalDay_ChangesAtTheDayChangeHourInLisbon(string instant, string? hour, string expected)
    {
        // O «hoje» das Boquilhas (sweep de 19/10/2026, B-1), o mesmo do painel,
        // da procura e dos registos: às 03:00 ainda é ontem; às 08:00 (07:00
        // UTC no verão) é hoje; com a viragem às 6h, às 06:00 já é hoje.
        using var db = await CreateContextAsync();
        var utc = DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);
        IModuleSettingsQuery settings = hour is null ? new StubSettings() : new HourSettings(hour);

        var day = await new BoquilhasCurrentQuery(db, settings, new FixedClock(utc)).OperationalDayAsync();

        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), day);
    }

    [Fact]
    public async Task Card_ReflectsBqIdPlannedByTheJobOn()
    {
        // O card da B1 reflete a BOQUILHA PLANEADA NO JOB ON (o BqContext —
        // a herança do canal jobon), não uma escolha do módulo.
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        db.Machines.AddRange(b1, b2);

        db.Tools.Add(new Tool { ToolId = "TA1", Type = ToolType.Bq, Reference = "C142", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "TA2", Type = ToolType.Bq, Reference = "T194", Lot = "12" });

        // O 202601: o primeiro da B1 com a T194; o 202602: o corrente com a C142.
        var job1 = new JobOn { ProductionNumber = "202601", Reference = "1075", Machine = b1 };
        var job2 = new JobOn { ProductionNumber = "202602", Reference = "1075", Machine = b1 };
        db.JobOns.AddRange(job1, job2);
        await db.SaveChangesAsync();

        db.BqContexts.Add(new BqContext { JobOnId = job1.Id, ToolId = "TA2" });
        db.BqContexts.Add(new BqContext { JobOnId = job2.Id, ToolId = "TA1" });
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var cardB1 = currents.Single(c => c.MachineCode == "B1");
        Assert.Equal("202602", cardB1.ProductionNumber);
        Assert.Equal("C142", cardB1.BqReference);
        Assert.Equal("TA1", cardB1.BqToolId);
    }

    [Fact]
    public async Task DatedJobOn_CoveringOperationalDay_IsTheCurrent()
    {
        // A REGRA F4-4 COMPLETA (o corte B): a produção cuja JANELA
        // TEMPORAL cobre o DIA OPERACIONAL é a corrente — mesmo quando um
        // Job On mais RECENTE existe com datas que NÃO cobrem (o futuro
        // nunca é corrente).
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);

        db.Tools.Add(new Tool { ToolId = "TA1", Type = ToolType.Bq, Reference = "C142", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "TA2", Type = ToolType.Bq, Reference = "T194", Lot = "12" });

        var today = DateOnly.FromDateTime(DmoBusinessTime.Now);

        // O 202601: a janela que COBRE hoje (a corrente pela regra).
        var covering = new JobOn
        {
            ProductionNumber = "202601",
            Reference = "1075",
            Machine = b1,
            InicioEm = today.AddDays(-3),
            FimEm = today.AddDays(3)
        };
        // O 202602: mais recente MAS a janela começa AMANHÃ (o futuro —
        // nunca a corrente: «nunca a mais recente futura»).
        var future = new JobOn
        {
            ProductionNumber = "202602",
            Reference = "1075",
            Machine = b1,
            InicioEm = today.AddDays(1)
        };
        db.JobOns.AddRange(covering, future);
        await db.SaveChangesAsync();

        db.BqContexts.Add(new BqContext { JobOnId = covering.Id, ToolId = "TA1" });
        db.BqContexts.Add(new BqContext { JobOnId = future.Id, ToolId = "TA2" });
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var cardB1 = currents.Single(c => c.MachineCode == "B1");
        Assert.Equal("202601", cardB1.ProductionNumber);
        Assert.Equal("C142", cardB1.BqReference);
    }

    [Fact]
    public async Task DiaDaTroca_AProducaoQueEntra_EACorrente_MesmoRegistadaAntes()
    {
        // Diogo, 21/10/2026: «uma producao sai dia 20 outra entra dia 20». A
        // que entra foi registada primeiro; a que sai, depois. No dia 20 a
        // máquina tem a que entra, com a BQ dela (a linha ficava vazia).
        using var db = await CreateContextAsync();
        var c3 = new Machine { Code = "C3" };
        db.Machines.Add(c3);
        db.Tools.Add(new Tool { ToolId = "BQ-ENTRA", Type = ToolType.Bq, Reference = "T324", Lot = "5" });
        var dia = new DateOnly(2026, 10, 20);
        var entra = new JobOn { ProductionNumber = "202604", Reference = "5538T324", Machine = c3, InicioEm = dia };
        db.JobOns.Add(entra);
        await db.SaveChangesAsync();
        var sai = new JobOn { ProductionNumber = "202603", Reference = "5612T173", Machine = c3, InicioEm = dia.AddDays(-10), FimEm = dia };
        db.JobOns.Add(sai);
        await db.SaveChangesAsync();
        db.BqContexts.Add(new BqContext { JobOnId = entra.Id, ToolId = "BQ-ENTRA" });
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings(), new FixedClock(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero)));
        // Diogo, 22/10/2026: as DUAS aparecem (a que sai também se pode
        // escolher para registar a BQ); a que entra primeiro; sem aviso, o
        // dia da troca não é sobreposição.
        var linhasC3 = (await query.ListAsync()).Where(c => c.MachineCode == "C3").ToList();

        Assert.Equal(["202604", "202603"], linhasC3.Select(c => c.ProductionNumber));
        Assert.Equal("T324", linhasC3[0].BqReference);
        Assert.All(linhasC3, c => Assert.False(c.Overlap));
    }

    [Fact]
    public async Task SobreposicaoReal_MostraAsDuasProducoes_ComAviso()
    {
        // Duas janelas que se cruzam de verdade (JobOnOverlap.Crosses): as
        // duas aparecem, a que entrou por último primeiro, e a linha leva o
        // aviso. Nada bloqueia.
        using var db = await CreateContextAsync();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        var dia = new DateOnly(2026, 10, 20);
        db.JobOns.AddRange(
            new JobOn { ProductionNumber = "202610", Reference = "A", Machine = b1, InicioEm = dia.AddDays(-5), FimEm = dia.AddDays(3) },
            new JobOn { ProductionNumber = "202611", Reference = "B", Machine = b1, InicioEm = dia.AddDays(-1), FimEm = dia.AddDays(5) });
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings(), new FixedClock(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero)));
        var linhasB1 = (await query.ListAsync()).Where(c => c.MachineCode == "B1").ToList();

        Assert.Equal(["202611", "202610"], linhasB1.Select(c => c.ProductionNumber));
        Assert.All(linhasB1, c => Assert.True(c.Overlap));
    }

    [Fact]
    public async Task UmaSoProducao_UmaLinha_SemAviso()
    {
        using var db = await CreateContextAsync();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        var dia = new DateOnly(2026, 10, 20);
        db.JobOns.Add(new JobOn { ProductionNumber = "202612", Reference = "A", Machine = b1, InicioEm = dia.AddDays(-2), FimEm = dia.AddDays(2) });
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings(), new FixedClock(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero)));
        var linha = Assert.Single(await query.ListAsync());

        Assert.Equal("202612", linha.ProductionNumber);
        Assert.False(linha.Overlap);
    }

    [Fact]
    public async Task DatedJobOn_EndedBeforeOperationalDay_FallsBackToUndated()
    {
        // A janela que JÁ PASSOU não é corrente; o FALLBACK de transição
        // devolve a última produção SEM datas (os Job Ons anteriores ao
        // corte B); sem nada, «Sem produção».
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);

        var today = DateOnly.FromDateTime(DmoBusinessTime.Now);

        // O 202601: a janela TERMINOU há dias (não cobre) — o fim fica DOIS
        // dias atrás para o teste ser determinístico em QUALQUER hora do
        // dia: antes da viragem das 8h o dia operacional corrente ainda é
        // ontem, e um fim «ontem» passaria a cobri-lo (flake temporal).
        var ended = new JobOn
        {
            ProductionNumber = "202601",
            Reference = "1075",
            Machine = b1,
            InicioEm = today.AddDays(-10),
            FimEm = today.AddDays(-2)
        };
        // O 202602: SEM datas (o fallback de transição).
        var undated = new JobOn { ProductionNumber = "202602", Reference = "1075", Machine = b1 };
        db.JobOns.AddRange(ended, undated);
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var cardB1 = currents.Single(c => c.MachineCode == "B1");
        Assert.Equal("202602", cardB1.ProductionNumber);
    }

    [Fact]
    public async Task MachineWithoutProduction_ShowsNoProduction()
    {
        // «Sem produção» — nunca a mais recente de outra máquina (o fallback
        // nunca inventa estado).
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        db.Machines.AddRange(b1, c3);
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var cardC3 = currents.Single(c => c.MachineCode == "C3");
        Assert.Null(cardC3.ProductionNumber);
        Assert.Null(cardC3.BqReference);
    }

    [Fact]
    public async Task JobOnWithoutBqContext_ShowsProductionWithoutBq()
    {
        // Uma produção real do Job On SEM boquilha no contexto: o card mostra
        // a produção e «Sem produção» na boquilha (nada é inventado).
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        var job = new JobOn { ProductionNumber = "202603", Reference = "5447T173", Machine = b1 };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var card = currents.Single(c => c.MachineCode == "B1");
        Assert.Equal("202603", card.ProductionNumber);
        Assert.Null(card.BqReference);
    }

    [Fact]
    public async Task Card_WithCurrentBq_CarriesBqReferenceContextAndMachine()
    {
        // A BQ da produção corrente vem na mesma consulta das candidatas:
        // a referência da Tool, o tool_id, o bq_id e o id da máquina.
        using var db = await CreateContextAsync();

        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        db.Tools.Add(new Tool { ToolId = "TA1", Type = ToolType.Bq, Reference = "C142", Lot = "1", BqAccountedQuantity = 120 });
        var job = new JobOn { ProductionNumber = "202601", Reference = "1075", Machine = b1 };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();

        var bq = new BqContext { JobOnId = job.Id, ToolId = "TA1" };
        db.BqContexts.Add(bq);
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var card = currents.Single(c => c.MachineCode == "B1");
        Assert.Equal(b1.Id, card.MachineId);
        Assert.Equal(job.Id, card.JobOnId);
        Assert.Equal("C142", card.BqReference);
        Assert.Equal("TA1", card.BqToolId);
        Assert.Equal(bq.Id, card.BqContextId);
    }

    [Fact]
    public async Task Card_WithoutCurrentBq_HasNoBq()
    {
        // SEM produção corrente: «Sem produção» — sem BQ nem bq_id.
        using var db = await CreateContextAsync();

        var c3 = new Machine { Code = "C3" };
        db.Machines.Add(c3);
        await db.SaveChangesAsync();

        var query = new BoquilhasCurrentQuery(db, new StubSettings());
        var currents = await query.ListAsync();

        var card = currents.Single(c => c.MachineCode == "C3");
        Assert.Equal(c3.Id, card.MachineId);
        Assert.Null(card.BqReference);
        Assert.Null(card.BqContextId);
    }
}

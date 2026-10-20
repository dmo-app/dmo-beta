using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova o READ MODEL DO RESUMO DIÁRIO · 6 MÁQUINAS e os movimentos
/// detalhados do Histórico (o historyTab do dmo-html): uma entrada por
/// (dia, máquina, REGISTO) com os contadores E/S/SR — cada referência
/// aparece separada quando há mudança de registo na mesma máquina; os
/// movimentos sem máquina resolvível não entram na grelha (não têm
/// coluna) e continuam na lista detalhada; TUDO sobre a DATA REAL do
/// movimento (o occurredAt da A1 — o backfill conta no seu dia). Os
/// filtros do html (a máquina, o tipo, desde/até, o reparador, a pesquisa
/// de texto) filtram sobre a data real. O read model é o que o gerador
/// PDF da A5 consome — o gerador SÓ FORMATA, nunca calcula.
/// </summary>
public sealed class BqHistorySummaryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly BqToolHistoryQuery _query;

    public BqHistorySummaryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new BqToolHistoryQuery(_db);
    }

    /// <summary>A grelha como a página a monta: os movimentos filtrados, agrupados pelo resumo diário.</summary>
    private async Task<IReadOnlyList<BqDailySummaryEntry>> SummaryAsync(
        DateOnly desde, DateOnly ate, string? machine, string? type, string? operatorId, string? text) =>
        BqDailySummary.FromMovements(await _query.ListMovementsAsync(desde, ate, machine, type, operatorId, text));

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>O dia retroativo em hora local → UTC (o mesmo caminho do
    /// form datetime-local → a folha), há N dias às 09:00 locais.</summary>
    private static DateTime DaysAgoUtc(int days, int hour = 9) =>
        DmoBusinessTime.ToUtc(
            DateTime.SpecifyKind(DmoBusinessTime.Now.AddDays(-days).Date.AddHours(hour), DateTimeKind.Unspecified));

    private async Task<(Machine B1, Machine B2)> SeedMachinesAsync()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        _db.Machines.AddRange(b1, b2);
        await _db.SaveChangesAsync();
        return (b1, b2);
    }

    private async Task<Tool> SeedToolAsync(string toolId, string reference, string lot)
    {
        var tool = new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = reference,
            Lot = lot
        };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqContext> SeedProductionAsync(
        Tool tool, Machine machine, string productionNumber)
    {
        var jobOn = new JobOn
        {
            Reference = tool.Reference,
            ProductionNumber = productionNumber,
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = tool.ToolId };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        return bq;
    }

    private async Task<BqRepairTrace> SeedAssociatedTraceAsync(BqContext bq)
    {
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    private async Task<BqRepairTrace> SeedAvulsoTraceAsync(Tool tool)
    {
        var trace = new BqRepairTrace { ToolId = tool.ToolId, BqContextId = null };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    private async Task MovementAsync(
        BqRepairTrace trace, BqMovementType type, int quantity, DateTime occurredUtc)
    {
        _db.BqMovements.Add(new BqMovement
        {
            BqRepairTraceId = trace.Id,
            Type = type,
            Quantity = quantity,
            OccurredAtUtc = occurredUtc,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("2026-07-31T23:30:00Z", "2026-08-01")]
    [InlineData("2026-01-31T23:30:00Z", "2026-01-31")]
    public async Task HistoryAndCalendar_UseTheLisbonDateAtTheMonthBoundary(string instant, string localDay)
    {
        var (machine, _) = await SeedMachinesAsync();
        var tool = await SeedToolAsync("BQ-TIME", "T173", "1");
        var context = await SeedProductionAsync(tool, machine, "TIME-01");
        var trace = await SeedAssociatedTraceAsync(context);
        var utc = DateTime.Parse(instant, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var day = DateOnly.ParseExact(localDay, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await MovementAsync(trace, BqMovementType.Saida, 12, utc);

        Assert.Single(await _query.ListMovementsAsync(day, day, null, null, null, null));
        Assert.Empty(await _query.ListMovementsAsync(day.AddDays(-1), day.AddDays(-1), null, null, null, null));
        var month = Assert.Single(await _query.ListMonthCountsAsync(day.Year, day.Month));
        Assert.Equal(day, month.Day);
        var summary = Assert.Single(await SummaryAsync(day, day, null, null, null, null));
        Assert.Equal(day, summary.Day);
        Assert.Equal(1, summary.Saidas);
        Assert.Equal(12, summary.SaidasQuantidade);
        Assert.Equal(utc, Assert.Single(await _query.ListMovementsAsync(day, day, null, null, null, null)).OccurredAtUtc);
    }

    [Fact]
    public async Task DailySummary_GroupsByDayMachineAndTrace_WithEsrCounts_OnTheRealDate()
    {
        var (b1, b2) = await SeedMachinesAsync();
        var toolA = await SeedToolAsync("BQ-A", "9389T194", "12");
        var toolB = await SeedToolAsync("BQ-B", "5447T173", "24/33");
        var bqA = await SeedProductionAsync(toolA, b1, "202601");
        var traceA = await SeedAssociatedTraceAsync(bqA);

        // O toolB AVULSO com a máquina associada da ficha (a rota) — o
        // ToolMachineLink dá a máquina à grelha (a B2).
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-B", MachineId = b2.Id });
        await _db.SaveChangesAsync();
        var traceB1 = await SeedAvulsoTraceAsync(toolB);
        var traceB2 = await SeedAvulsoTraceAsync(toolB);

        var day2 = DaysAgoUtc(2, 8);   // há 2 dias, 08:00
        var day1 = DaysAgoUtc(1, 10);  // há 1 dia, 10:00

        // O registo A na B1, há 2 dias: uma Saída e uma Entrada.
        await MovementAsync(traceA, BqMovementType.Saida, 2, day2);
        await MovementAsync(traceA, BqMovementType.Entrada, 1, day2.AddHours(2));

        // O MESMO TOOL em DOIS REGISTOS na B2 no mesmo dia: cada registo
        // aparece SEPARADO (a mudança de registo na mesma máquina — o
        // html agrupa pelo link).
        await MovementAsync(traceB1, BqMovementType.Saida, 1, day1);
        await MovementAsync(traceB2, BqMovementType.Saida, 2, day1.AddHours(2));

        // Há 1 dia: a entrada do registo B1 na B2 (o trace avulso).
        await MovementAsync(traceB1, BqMovementType.EntradaSemReparacao, 1, day1.AddHours(4));

        var entries = await SummaryAsync(
            DateOnly.FromDateTime(DmoBusinessTime.Now.AddDays(-3)),
            DateOnly.FromDateTime(DmoBusinessTime.Now),
            null, null, null, null);

        // UMA entrada por (dia, máquina, registo) — 3 no total: o registo A
        // no dia 2 (B1) e os DOIS registos do mesmo tool no dia 1 (B2), o
        // SR do registo B1 FUNDE na entrada dele (o agrupamento é pelo
        // registo, o link do html).
        Assert.Equal(3, entries.Count);

        var day2B1 = Assert.Single(entries, e =>
            e.Day == DateOnly.FromDateTime(DmoBusinessTime.ToLocal(day2))
            && e.MachineCode == "B1");
        Assert.Equal(traceA.Id, day2B1.BqRepairTraceId);
        Assert.Equal(1, day2B1.Saidas);
        Assert.Equal(1, day2B1.Entradas);
        Assert.Equal(0, day2B1.EntradasSemReparacao);
        Assert.Equal("9389T194", day2B1.ToolReference);
        Assert.Equal("12", day2B1.ToolLot);
        Assert.Equal("202601", day2B1.ProductionNumber);

        var day1B2 = entries
            .Where(e => e.Day == DateOnly.FromDateTime(DmoBusinessTime.ToLocal(day1))
                && e.MachineCode == "B2")
            .OrderBy(e => e.BqRepairTraceId)
            .ToList();
        Assert.Equal(2, day1B2.Count); // DOIS registos do mesmo tool, separados
        Assert.All(day1B2, e => Assert.Equal("5447T173", e.ToolReference));

        // A entrada_sem_reparacao conta em SR e NÃO em E.
        var srEntry = Assert.Single(entries, e => e.EntradasSemReparacao == 1);
        Assert.Equal(0, srEntry.Entradas);
        Assert.Equal(1, srEntry.EntradasSemReparacao);

        // A ORDEM: dia asc, máquina asc (o mesmo dia B1 antes de B2).
        var ordered = entries.OrderBy(e => e.Day).ThenBy(e => e.MachineCode).ToList();
        Assert.Equal(ordered.Select(e => (e.Day, e.MachineCode, e.BqRepairTraceId)), entries.Select(e => (e.Day, e.MachineCode, e.BqRepairTraceId)));
    }

    [Fact]
    public async Task DailySummary_ExcludesMovementsWithoutMachine_KeepsThemInTheDetailedList()
    {
        var (b1, _) = await SeedMachinesAsync();
        var toolA = await SeedToolAsync("BQ-A", "9389T194", "12");
        var bqA = await SeedProductionAsync(toolA, b1, "202601");
        var traceA = await SeedAssociatedTraceAsync(bqA);
        var traceAvulso = await SeedAvulsoTraceAsync(toolA); // SEM ToolMachineLink

        var when = DaysAgoUtc(1, 8);
        await MovementAsync(traceA, BqMovementType.Saida, 2, when);
        await MovementAsync(traceAvulso, BqMovementType.Saida, 3, when.AddHours(1));

        var day = DateOnly.FromDateTime(DmoBusinessTime.ToLocal(when));
        var entries = await SummaryAsync(day, day, null, null, null, null);

        // A GRELHA é dia×máquina: o movimento sem máquina não tem coluna —
        // só o registo com máquina entra.
        var entry = Assert.Single(entries);
        Assert.Equal(traceA.Id, entry.BqRepairTraceId);
        Assert.Equal(1, entry.Saidas);

        // A LISTA DETALHADA mostra TODOS (o «—» na máquina).
        var movements = await _query.ListMovementsAsync(day, day, null, null, null, null);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.NotEqual(default, m.OccurredAtUtc));
        Assert.Contains(movements, m => m.MachineCode == null);
    }

    [Fact]
    public async Task MovementsList_FiltersByRangeTypeAndText_OverTheRealDate()
    {
        var (b1, _) = await SeedMachinesAsync();
        var toolA = await SeedToolAsync("BQ-A", "9389T194", "12");
        var toolB = await SeedToolAsync("BQ-B", "5447T173", "24/33");
        var bqA = await SeedProductionAsync(toolA, b1, "202601");
        var traceA = await SeedAssociatedTraceAsync(bqA);
        var traceB = await SeedAvulsoTraceAsync(toolB);

        var old = DaysAgoUtc(40, 8);   // há 40 dias — fora do intervalo curto
        var recent = DaysAgoUtc(2, 9); // há 2 dias — dentro
        await MovementAsync(traceA, BqMovementType.Saida, 2, old);
        await MovementAsync(traceA, BqMovementType.Entrada, 1, recent);
        await MovementAsync(traceB, BqMovementType.Saida, 5, recent.AddHours(1));

        var desde = DateOnly.FromDateTime(DmoBusinessTime.Now.AddDays(-5));
        var ate = DateOnly.FromDateTime(DmoBusinessTime.Now);

        // O INTERVALO sobre a data REAL: o movimento de há 40 dias fica fora.
        var inRange = await _query.ListMovementsAsync(desde, ate, null, null, null, null);
        Assert.Equal(2, inRange.Count);
        Assert.DoesNotContain(inRange, m => m.OccurredAtUtc < DmoBusinessTime.ToUtc(desde.ToDateTime(TimeOnly.MinValue)));

        // O TIPO: só as saídas do intervalo.
        var saidas = await _query.ListMovementsAsync(desde, ate, null, "saida", null, null);
        var saida = Assert.Single(saidas);
        Assert.Equal(5, saida.Quantity);
        Assert.Equal("5447T173", saida.ToolReference);

        // A PESQUISA de texto: a referência da tool A (o case-insensitive).
        var byText = await _query.ListMovementsAsync(desde, ate, null, null, null, "9389t194");
        var movementA = Assert.Single(byText);
        Assert.Equal("9389T194", movementA.ToolReference);

        // A MÁQUINA: a produção do A está na B1.
        var byMachine = await _query.ListMovementsAsync(desde, ate, "B1", null, null, null);
        Assert.Single(byMachine, m => m.ToolReference == "9389T194");

        // UM TOKEN DE TIPO DESCONHECIDO NÃO BLOQUEIA: cai no «todos».
        var unknown = await _query.ListMovementsAsync(desde, ate, null, "irreparavel", null, null);
        Assert.Equal(2, unknown.Count);
    }
}

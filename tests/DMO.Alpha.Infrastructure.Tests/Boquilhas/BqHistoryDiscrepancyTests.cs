using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// O Saldo (Discrepancy) do Histórico das Boquilhas, sobre duas regras que
/// não dependem da máquina/produção: BQ-B02 (cada BQ eliminada é a sua
/// própria conta, nunca a mesma «REMOVED» de outra BQ eliminada) e BQ-B03
/// (o «meter a zero» de um registo esconde o Saldo das linhas de antes do
/// zero, tal como o Registo mostra).
/// </summary>
public sealed class BqHistoryDiscrepancyTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly BqToolHistoryQuery _query;

    public BqHistoryDiscrepancyTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new BqToolHistoryQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Machine> SeedMachineAsync()
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<Tool> SeedToolAsync(string toolId, string reference, string lot)
    {
        var tool = new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = reference, Lot = lot };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqRepairTrace> SeedDeletedBqTraceAsync(
        Machine machine, Tool tool, string productionNumber)
    {
        // A produção e o registo associado, como um registo normal.
        var jobOn = new JobOn { Reference = tool.Reference, ProductionNumber = productionNumber, Machine = machine };
        var bq = new BqContext { JobOn = jobOn, ToolId = tool.ToolId };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        // A eliminação da Tool (DeleteToolHandler): o contexto fica com o
        // tool_id REMOVED e o trace guarda a referência/lote congelados.
        bq.ToolId = ToolRegistryConstants.RemovedToolId;
        trace.ToolReferenceFrozen = tool.Reference;
        trace.ToolLotFrozen = tool.Lot;
        await _db.SaveChangesAsync();
        return trace;
    }

    private async Task<BqMovement> MovementAsync(BqRepairTrace trace, BqMovementType type, int quantity, DateTime occurredUtc, DateTime? createdUtc = null)
    {
        var movement = new BqMovement
        {
            BqRepairTraceId = trace.Id,
            Type = type,
            Quantity = quantity,
            OccurredAtUtc = occurredUtc,
            CreatedAtUtc = createdUtc ?? occurredUtc
        };
        _db.BqMovements.Add(movement);
        await _db.SaveChangesAsync();
        return movement;
    }

    [Fact]
    public async Task DuasBqsEliminadas_TemSaldosSeparados_BQ_B02()
    {
        var machine = await SeedMachineAsync();
        var toolX = await SeedToolAsync("BQ-X", "T100", "1");
        var toolY = await SeedToolAsync("BQ-Y", "T200", "2");
        var traceX = await SeedDeletedBqTraceAsync(machine, toolX, "PROD-X");
        var traceY = await SeedDeletedBqTraceAsync(machine, toolY, "PROD-Y");

        var day = DateTime.UtcNow.Date;
        // A BQ-X saiu (2) — fica outstanding.
        await MovementAsync(traceX, BqMovementType.Saida, 2, day.AddHours(8));
        // A BQ-Y entra (5) sem nenhuma saída sua: sozinha, fica tudo por
        // explicar (-5). Se as duas BQs eliminadas caíssem na mesma conta
        // «REMOVED» (BQ-B02), a saída da BQ-X (2) explicava parte desta
        // entrada e a discrepância ficaria errada (-3).
        var entradaY = await MovementAsync(traceY, BqMovementType.Entrada, 5, day.AddHours(9));

        var items = await _query.ListMovementsAsync(
            DateOnly.FromDateTime(day), DateOnly.FromDateTime(day).AddDays(1), null, null, null, null);

        var item = Assert.Single(items, i => i.MovementId == entradaY.Id);
        Assert.Equal(-5, item.Discrepancy);
    }

    [Fact]
    public async Task MeterAZero_EscondeOSaldoDasLinhasDeAntes_BQ_B03()
    {
        var machine = await SeedMachineAsync();
        var tool = await SeedToolAsync("BQ-Z", "T300", "3");
        var jobOn = new JobOn { Reference = tool.Reference, ProductionNumber = "PROD-Z", Machine = machine };
        var bq = new BqContext { JobOn = jobOn, ToolId = tool.ToolId };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var resetAtUtc = DateTime.UtcNow.Date.AddHours(12);
        trace.DiscrepancyResetAtUtc = resetAtUtc;
        await _db.SaveChangesAsync();

        // Duas entradas sem nenhuma saída: cada uma fica, sozinha, toda por
        // explicar — a de antes do zero teria -4, a de depois -2.
        var antes = await MovementAsync(trace, BqMovementType.Entrada, 4, resetAtUtc.AddHours(-2));
        var depois = await MovementAsync(trace, BqMovementType.Entrada, 2, resetAtUtc.AddHours(2));

        var items = await _query.ListMovementsAsync(
            DateOnly.FromDateTime(resetAtUtc.AddDays(-1)), DateOnly.FromDateTime(resetAtUtc.AddDays(1)), null, null, null, null);

        var itemAntes = Assert.Single(items, i => i.MovementId == antes.Id);
        var itemDepois = Assert.Single(items, i => i.MovementId == depois.Id);

        // BQ-B03: o Histórico esconde o Saldo da linha de antes do zero
        // (null), tal como o Registo; a de depois do zero continua a mostrar.
        Assert.Null(itemAntes.Discrepancy);
        Assert.Equal(-2, itemDepois.Discrepancy);
    }
}

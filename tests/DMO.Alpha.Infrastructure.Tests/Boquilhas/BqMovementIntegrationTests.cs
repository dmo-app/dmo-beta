using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova a persistência canónica dos movimentos de Boquilhas: os três tipos
/// autorizados persistem com a quantidade observada na totalidade e a
/// discrepância produzida como facto histórico; um movimento resolve o seu
/// bq_repair_trace_id; vários movimentos pertencem ao mesmo trace (incluindo
/// trace pré-produção, sem bq_id); gravar movimentos não altera dados do
/// Job On, do BqContext ou do próprio trace.
/// </summary>
public sealed class BqMovementIntegrationTests : IDisposable
{
    private readonly DmoDbContext _db;

    public BqMovementIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Tool> SeedBqToolAsync(string toolId)
    {
        var tool = new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "12"
        };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqRepairTrace> SeedTraceAsync(string toolId)
    {
        var trace = new BqRepairTrace { ToolId = toolId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    [Fact]
    public async Task AllThreeMovementTypes_PersistWithObservedQuantityAndProducedDiscrepancy()
    {
        await SeedBqToolAsync("BQ-TOOL-9");
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        _db.BqMovements.AddRange(
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Saida,
                Quantity = 5,
                Discrepancy = null
            },
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Entrada,
                Quantity = 7,
                Discrepancy = -2
            },
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.EntradaSemReparacao,
                Quantity = 3,
                Discrepancy = null
            });
        await _db.SaveChangesAsync();

        var movements = await _db.BqMovements
            .AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .ToListAsync();

        Assert.Equal(3, movements.Count);
        Assert.Contains(movements, m =>
            m.Type == BqMovementType.Saida && m.Quantity == 5 && m.Discrepancy is null);
        // A Entrada observada persiste na totalidade (7) com a discrepância
        // produzida (-2); nada é truncado, compensado ou reescrito.
        Assert.Contains(movements, m =>
            m.Type == BqMovementType.Entrada && m.Quantity == 7 && m.Discrepancy == -2);
        Assert.Contains(movements, m =>
            m.Type == BqMovementType.EntradaSemReparacao && m.Quantity == 3 && m.Discrepancy is null);
        Assert.Equal(3, movements.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task Movement_ResolvesItsTrace()
    {
        await SeedBqToolAsync("BQ-TOOL-9");
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var movement = new BqMovement
        {
            BqRepairTraceId = trace.Id,
            Type = BqMovementType.Saida,
            Quantity = 5
        };
        _db.BqMovements.Add(movement);
        await _db.SaveChangesAsync();

        var resolved = await _db.BqMovements
            .AsNoTracking()
            .Include(m => m.Trace)
            .ThenInclude(t => t.Tool)
            .SingleAsync(m => m.Id == movement.Id);

        Assert.Equal(trace.Id, resolved.BqRepairTraceId);
        Assert.Equal(trace.Id, resolved.Trace.Id);
        // O movimento resolve o trace e, através dele, o tool_id canónico.
        Assert.Equal("BQ-TOOL-9", resolved.Trace.ToolId);
        Assert.NotNull(resolved.Trace.Tool);
        Assert.Equal("BQ-TOOL-9", resolved.Trace.Tool!.ToolId);
    }

    [Fact]
    public async Task MultipleMovements_BelongToOnePreProductionTrace()
    {
        // Vários ciclos de movimento pertencem ao mesmo bq_repair_trace_id,
        // incluindo num trace pré-produção (bq_id não resolvido): o trace
        // continua independente do Job On e nenhum estado é criado pelos
        // movimentos.
        await SeedBqToolAsync("BQ-TOOL-9");
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        _db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 5 },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 5 },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 4 },
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.EntradaSemReparacao,
                Quantity = 4
            });
        await _db.SaveChangesAsync();

        var movements = await _db.BqMovements
            .AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .ToListAsync();

        Assert.Equal(4, movements.Count);
        Assert.All(movements, m => Assert.Equal(trace.Id, m.BqRepairTraceId));
        Assert.Equal(4, movements.Select(m => m.Id).Distinct().Count());

        var storedTrace = await _db.BqRepairTraces
            .AsNoTracking()
            .SingleAsync(t => t.Id == trace.Id);
        Assert.Equal("BQ-TOOL-9", storedTrace.ToolId);
        Assert.Null(storedTrace.BqContextId);
    }

    [Fact]
    public async Task Movements_DoNotChangeJobOnOrBqContextData()
    {
        await SeedBqToolAsync("BQ-TOOL-9");

        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = "PN-202609",
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9", BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var jobOnId = jobOn.Id;
        var machineId = jobOn.MachineId;

        _db.BqMovements.AddRange(
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Saida,
                Quantity = 5
            },
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Entrada,
                Quantity = 7,
                Discrepancy = -2
            });
        await _db.SaveChangesAsync();

        // Gravar movimentos não escreve nada de volta no Job On, no
        // BqContext nem no trace.
        var jobOnAfter = await _db.JobOns
            .AsNoTracking()
            .SingleAsync(j => j.Id == jobOnId);
        Assert.Equal("9389T194", jobOnAfter.Reference);
        Assert.Equal("PN-202609", jobOnAfter.ProductionNumber);
        Assert.Equal(machineId, jobOnAfter.MachineId);

        var contextAfter = await _db.BqContexts
            .AsNoTracking()
            .SingleAsync(c => c.Id == bq.Id);
        Assert.Equal(jobOnId, contextAfter.JobOnId);
        Assert.Equal("BQ-TOOL-9", contextAfter.ToolId);

        var traceAfter = await _db.BqRepairTraces
            .AsNoTracking()
            .SingleAsync(t => t.Id == trace.Id);
        Assert.Equal("BQ-TOOL-9", traceAfter.ToolId);
        Assert.Equal(bq.Id, traceAfter.BqContextId);

        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.BqContexts.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.BqMovements.AsNoTracking().CountAsync());
    }
}

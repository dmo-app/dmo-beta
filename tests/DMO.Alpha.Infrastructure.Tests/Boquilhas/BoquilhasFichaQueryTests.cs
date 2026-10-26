using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// As leituras da ficha da BQ que a página Boquilhas fazia contra o
/// contexto: as mesmas respostas de antes, e nada é gravado.
/// </summary>
public sealed class BoquilhasFichaQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task MaquinasDaFerramenta_VemPorOrdemDoIdDaMaquina_SoDessaFerramenta()
    {
        var c2 = new Machine { Code = "C2" };
        var b1 = new Machine { Code = "B1" };
        var a3 = new Machine { Code = "A3" };
        _db.Machines.AddRange(c2, b1, a3);
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        await _db.SaveChangesAsync();
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "BQ-1", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "BQ-1", MachineId = c2.Id },
            new ToolMachineLink { ToolId = "BQ-2", MachineId = a3.Id });
        await _db.SaveChangesAsync();
        var query = new BoquilhasFichaQuery(_db);

        var maquinas = await query.MaquinasDaFerramentaAsync("BQ-1");

        // Ordem do id (C2 foi criada antes de B1), não do código.
        Assert.Equal(new[] { "C2", "B1" }, maquinas.Select(m => m.Code));
        Assert.Equal(new[] { c2.Id, b1.Id }, maquinas.Select(m => m.MachineId));
        Assert.Empty(await query.MaquinasDaFerramentaAsync("BQ-SEM-LIGACOES"));
    }

    [Fact]
    public async Task Ferramenta_DevolveReferenciaLoteEQuantidadeESemFerramentaDevolveNulo()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "7", BqAccountedQuantity = 60 });
        _db.Tools.Add(new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        await _db.SaveChangesAsync();
        var query = new BoquilhasFichaQuery(_db);

        Assert.Equal(new BqFichaFerramenta("T194", "7", 60), await query.FerramentaAsync("BQ-1"));
        Assert.Null((await query.FerramentaAsync("BQ-2"))!.AccountedQuantity);
        Assert.Null(await query.FerramentaAsync("BQ-X"));
    }

    [Fact]
    public async Task MovimentosSemJobOn_SoDestaBqSemJobOn_MaisRecentesPrimeiro_ComLimite()
    {
        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn { Machine = machine, Reference = "5447T194", ProductionNumber = "202605" };
        var contexto = new BqContext { JobOn = jobOn, ToolId = "BQ-1" };
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(contexto);
        await _db.SaveChangesAsync();
        var semJobOn = new BqRepairTrace { ToolId = "BQ-1" };
        var comJobOn = new BqRepairTrace { BqContextId = contexto.Id };
        var outra = new BqRepairTrace { ToolId = "BQ-2" };
        // Um registo já associado a um Job On que ainda guarda a âncora direta:
        // tem Job On, por isso não é «sem Job On».
        var associadoComAncora = new BqRepairTrace { ToolId = "BQ-1", BqContextId = contexto.Id };
        _db.BqRepairTraces.AddRange(semJobOn, comJobOn, outra, associadoComAncora);
        await _db.SaveChangesAsync();
        DateTime Dia(int d) => new(2026, 10, d, 8, 0, 0, DateTimeKind.Utc);
        _db.BqMovements.AddRange(
            Movimento(semJobOn.Id, BqMovementType.Saida, 1, "B1", Dia(1)),
            Movimento(semJobOn.Id, BqMovementType.Entrada, 2, "C2", Dia(3)),
            Movimento(semJobOn.Id, BqMovementType.Saida, 3, null, Dia(2)),
            Movimento(comJobOn.Id, BqMovementType.Saida, 9, "B1", Dia(9)),
            Movimento(associadoComAncora.Id, BqMovementType.Saida, 7, "B1", Dia(10)),
            Movimento(outra.Id, BqMovementType.Saida, 8, "B1", Dia(8)));
        await _db.SaveChangesAsync();
        var query = new BoquilhasFichaQuery(_db);

        var todos = await query.MovimentosSemJobOnAsync("BQ-1", 20);
        var dois = await query.MovimentosSemJobOnAsync("BQ-1", 2);

        Assert.Equal(new[] { 2, 3, 1 }, todos.Select(m => m.Quantity));
        Assert.Equal(("C2", BqMovementType.Entrada, Dia(3)), (todos[0].MachineCode, todos[0].Type, todos[0].OccurredAtUtc));
        Assert.Null(todos[1].MachineCode);
        Assert.Equal(new[] { 2, 3 }, dois.Select(m => m.Quantity));
        Assert.Empty(await query.MovimentosSemJobOnAsync("BQ-SEM", 20));
    }

    [Fact]
    public async Task OsTresPedidos_NaoGravamNada()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();
        var query = new BoquilhasFichaQuery(_db);
        _db.ChangeTracker.Clear();

        await query.MaquinasDaFerramentaAsync("BQ-1");
        await query.FerramentaAsync("BQ-1");
        await query.MovimentosSemJobOnAsync("BQ-1", 20);

        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private static BqMovement Movimento(int traceId, BqMovementType type, int quantity, string? machine, DateTime at) =>
        new() { BqRepairTraceId = traceId, Type = type, Quantity = quantity, MachineCodeFrozen = machine, OccurredAtUtc = at };
}

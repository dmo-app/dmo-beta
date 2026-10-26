using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// As leituras de escolha e de filtro que a página Boquilhas fazia contra o
/// contexto: as mesmas respostas de antes, e nada é gravado.
/// </summary>
public sealed class BoquilhasEscolhasQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task BqSemJobOn_SoAsBqSemATombstone_PorReferenciaELote_ComMaquinasEUltimaLinha()
    {
        var b1 = new Machine { Code = "B1" };
        var a2 = new Machine { Code = "A2" };
        _db.Machines.AddRange(b1, a2);
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-B-2", Type = ToolType.Bq, Reference = "T194", Lot = "2", BqAccountedQuantity = 50, AssociatedMfReference = "9389" },
            new Tool { ToolId = "BQ-A-1", Type = ToolType.Bq, Reference = "T173", Lot = "1" },
            new Tool { ToolId = "BQ-B-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "CM-1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Bq, Reference = "T000", Lot = "0" });
        await _db.SaveChangesAsync();
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "BQ-B-2", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "BQ-B-2", MachineId = a2.Id });
        var trace = new BqRepairTrace { ToolId = "BQ-B-2" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        _db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 1, MachineCodeFrozen = "C1", OccurredAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc) },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 1, MachineCodeFrozen = "B3", OccurredAtUtc = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc) },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 1, MachineCodeFrozen = null, OccurredAtUtc = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc) });
        await _db.SaveChangesAsync();

        var lidas = await new BoquilhasEscolhasQuery(_db).BqSemJobOnAsync();

        Assert.Equal(new[] { "BQ-A-1", "BQ-B-1", "BQ-B-2" }, lidas.Select(l => l.ToolId));
        var completa = lidas.Single(l => l.ToolId == "BQ-B-2");
        Assert.Equal(("T194", "2", 50, "9389", "B3"), (completa.Reference, completa.Lot, completa.Quantity, completa.MfReference, completa.LastLine));
        Assert.Equal(new[] { "A2", "B1" }, completa.Machines);
        var vazia = lidas.Single(l => l.ToolId == "BQ-A-1");
        Assert.Empty(vazia.Machines);
        Assert.Null(vazia.LastLine);
        Assert.Null(vazia.Quantity);
    }

    [Fact]
    public async Task Ferramenta_DevolveAReferenciaEOLoteESemFerramentaDevolveNulo()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-B-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();
        var query = new BoquilhasEscolhasQuery(_db);

        Assert.Equal(new BqIdentificada("BQ-B-1", "T194", "1"), await query.FerramentaAsync("BQ-B-1"));
        Assert.Null(await query.FerramentaAsync("BQ-X"));
    }

    [Fact]
    public async Task BqsDaReferencia_SoBqDaReferenciaSemDistinguirMaiusculasEMisturarTipos()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "t194", Lot = "1" },
            new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T194", Lot = "02" },
            new Tool { ToolId = "BQ-3", Type = ToolType.Bq, Reference = "T173", Lot = "1" },
            new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();

        var bqs = await new BoquilhasEscolhasQuery(_db).BqsDaReferenciaAsync("T194");

        Assert.Equal(new[] { "BQ-1", "BQ-2" }, bqs.Select(b => b.ToolId).OrderBy(i => i));
    }

    [Fact]
    public async Task ProducaoDoFiltro_DevolveNumeroReferenciaELinhaESemProducaoDevolveNulo()
    {
        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn { Machine = machine, Reference = "5447T194", ProductionNumber = "202605" };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var query = new BoquilhasEscolhasQuery(_db);

        Assert.Equal(new ProducaoDoFiltro(jobOn.Id, "202605", "5447T194", "B1"), await query.ProducaoDoFiltroAsync(jobOn.Id));
        Assert.Null(await query.ProducaoDoFiltroAsync(jobOn.Id + 100));
    }

    [Fact]
    public async Task OsQuatroPedidos_NaoGravamNada()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();
        var query = new BoquilhasEscolhasQuery(_db);
        _db.ChangeTracker.Clear();

        await query.BqSemJobOnAsync();
        await query.FerramentaAsync("BQ-1");
        await query.BqsDaReferenciaAsync("T194");
        await query.ProducaoDoFiltroAsync(1);

        Assert.Empty(_db.ChangeTracker.Entries());
    }
}

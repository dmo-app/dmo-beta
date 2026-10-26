using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// The two lookups the Pegamentos page used to run against the DbContext
/// itself: same answers as before, and nothing is written.
/// </summary>
public sealed class PegamentosEscolhasQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task LotesDaReferencia_VemComOLoteMaisAltoPrimeiroSemMisturarTiposNemReferencias()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-5447-9", Type = ToolType.Cm, Reference = "5447", Lot = "9" },
            new Tool { ToolId = "CM-5447-10", Type = ToolType.Cm, Reference = "5447", Lot = "10" },
            new Tool { ToolId = "CM-5447-SL", Type = ToolType.Cm, Reference = "5447", Lot = "s/lote" },
            new Tool { ToolId = "CM-5448-1", Type = ToolType.Cm, Reference = "5448", Lot = "1" },
            new Tool { ToolId = "BQ-5447-1", Type = ToolType.Bq, Reference = "5447", Lot = "1" });
        await _db.SaveChangesAsync();

        var lotes = await new PegamentosEscolhasQuery(_db).LotesDaReferenciaAsync("5447", ToolType.Cm);

        Assert.Equal(new[] { "10", "9", "s/lote" }, lotes.Select(l => l.Lot));
        Assert.All(lotes, l => Assert.Equal(ToolType.Cm, l.Type));
    }

    [Fact]
    public async Task LotesDaReferencia_ReferenciaEmBranco_NaoDevolveNada()
    {
        _db.Tools.Add(new Tool { ToolId = "CM-5447-1", Type = ToolType.Cm, Reference = "5447", Lot = "1" });
        await _db.SaveChangesAsync();

        Assert.Empty(await new PegamentosEscolhasQuery(_db).LotesDaReferenciaAsync("  ", ToolType.Cm));
    }

    [Fact]
    public async Task CodigosDeMaquinas_VemPorOrdemDoCodigo()
    {
        _db.Machines.AddRange(new Machine { Code = "C3" }, new Machine { Code = "B1" }, new Machine { Code = "A2" });
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { "A2", "B1", "C3" }, await new PegamentosEscolhasQuery(_db).CodigosDeMaquinasAsync());
    }

    [Fact]
    public async Task OsDoisPedidos_NaoGravamNada()
    {
        _db.Tools.Add(new Tool { ToolId = "CM-5447-1", Type = ToolType.Cm, Reference = "5447", Lot = "1" });
        _db.Machines.Add(new Machine { Code = "B1" });
        await _db.SaveChangesAsync();
        var query = new PegamentosEscolhasQuery(_db);
        _db.ChangeTracker.Clear();

        await query.LotesDaReferenciaAsync("5447", ToolType.Cm);
        await query.CodigosDeMaquinasAsync();

        Assert.Empty(_db.ChangeTracker.Entries());
    }
}

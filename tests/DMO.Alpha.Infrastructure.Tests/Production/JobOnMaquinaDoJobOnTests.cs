using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The machine of a production, read by the Job On «Reparação» page through
/// the service instead of the DbContext: same answer as before, nothing written.
/// </summary>
public sealed class JobOnMaquinaDoJobOnTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task MaquinaDoJobOn_DevolveALinhaDaProducao_OuZeroQuandoNaoExiste()
    {
        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        var naB1 = new JobOn { Machine = b1, Reference = "5447T173", ProductionNumber = "202601" };
        var naC3 = new JobOn { Machine = c3, Reference = "5447T173", ProductionNumber = "202602" };
        _db.JobOns.AddRange(naB1, naC3);
        await _db.SaveChangesAsync();
        var query = new JobOnPaginaQuery(_db);

        Assert.Equal(b1.Id, await query.MaquinaDoJobOnAsync(naB1.Id));
        Assert.Equal(c3.Id, await query.MaquinaDoJobOnAsync(naC3.Id));
        Assert.Equal(0, await query.MaquinaDoJobOnAsync(naC3.Id + 100));
        Assert.Empty(_db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
    }
}

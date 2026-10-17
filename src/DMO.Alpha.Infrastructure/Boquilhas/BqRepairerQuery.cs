using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A leitura das entidades reparadoras para a Admin (Admin → App
/// Definições → Boquilhas): a lista do vocabulário administrado por
/// NOME (a ordem humana). Projeção mínima, AsNoTracking, sem scans.
/// </summary>
public sealed class BqRepairerQuery : IBqRepairerQuery
{
    private readonly DmoDbContext _db;

    public BqRepairerQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BqRepairerItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.BqRepairers
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new BqRepairerItem(r.Id, r.Name))
            .ToListAsync(cancellationToken);
    }
}

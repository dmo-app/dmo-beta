using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// The Pegamentos page lookups that used to read the DbContext from the page
/// itself. Each method is the same single read-only query as before.
/// </summary>
public sealed class PegamentosEscolhasQuery(DmoDbContext db) : IPegamentosEscolhasQuery
{
    public Task<IReadOnlyList<ToolLookupResult>> LotesDaReferenciaAsync(
        string reference,
        ToolType type,
        CancellationToken cancellationToken = default) =>
        ToolLookupQuery.ListByReferenceAsync(db, reference, type, cancellationToken);

    public async Task<IReadOnlyList<string>> CodigosDeMaquinasAsync(CancellationToken cancellationToken = default) =>
        await db.Machines.OrderBy(m => m.Code).Select(m => m.Code).ToListAsync(cancellationToken);
}

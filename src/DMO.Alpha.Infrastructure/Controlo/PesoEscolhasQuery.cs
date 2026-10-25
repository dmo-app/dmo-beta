using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// The Peso page lookups that used to read the DbContext from the page itself.
/// Each method is the same single read-only query as before.
/// </summary>
public sealed class PesoEscolhasQuery(DmoDbContext db) : IPesoEscolhasQuery
{
    public Task<int?> ControloDaProducaoAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        db.Controlos.AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> ProducaoDoControloAsync(int controloId, CancellationToken cancellationToken = default) =>
        db.Controlos.AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => (int?)c.JobOnId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> CodigosDeMaquinasAsync(CancellationToken cancellationToken = default) =>
        await db.Machines.AsNoTracking()
            .OrderBy(m => m.Code)
            .Select(m => m.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ComparacaoGuardadaExisteAsync(
        int comparacaoId,
        int pesoInicialId,
        CancellationToken cancellationToken = default) =>
        db.PesoComparacoes.AsNoTracking()
            .AnyAsync(c => c.Id == comparacaoId && c.PesoInicialId == pesoInicialId, cancellationToken);
}

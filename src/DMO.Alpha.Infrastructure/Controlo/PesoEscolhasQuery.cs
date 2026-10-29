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

    public async Task<IReadOnlyList<string>> CodigosDeMaquinasDaReferenciaAsync(string referencia, CancellationToken cancellationToken = default)
    {
        var daReferencia = await db.ToolMachineLinks.AsNoTracking()
            .Where(l => db.Tools.Any(t => t.ToolId == l.ToolId && (t.Reference == referencia || t.AssociatedMfReference == referencia)))
            .Join(db.Machines, l => l.MachineId, m => m.Id, (l, m) => m.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
        return daReferencia.Count > 0 ? daReferencia : await CodigosDeMaquinasAsync(cancellationToken);
    }

    public Task<bool> ComparacaoGuardadaExisteAsync(
        int comparacaoId,
        int pesoInicialId,
        CancellationToken cancellationToken = default) =>
        db.PesoComparacoes.AsNoTracking()
            .AnyAsync(c => c.Id == comparacaoId && c.PesoInicialId == pesoInicialId, cancellationToken);
}

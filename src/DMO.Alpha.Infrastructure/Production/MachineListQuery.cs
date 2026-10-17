using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação só de leitura da lista de máquinas de produção persistidas
/// para seleção humana explícita no Job On. Projeção mínima AsNoTracking
/// ordenada por code (determinística); não inventa máquinas, não muta
/// estado, não auto-seleciona.
/// </summary>
public sealed class MachineListQuery : IMachineListQuery
{
    private readonly DmoDbContext _db;

    public MachineListQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MachineOption>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Machines
            .AsNoTracking()
            .OrderBy(m => m.Code)
            .Select(m => new MachineOption(m.Id, m.Code))
            .ToListAsync(cancellationToken);
    }
}

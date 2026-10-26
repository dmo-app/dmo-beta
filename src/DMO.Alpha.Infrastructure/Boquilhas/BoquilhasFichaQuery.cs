using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// As leituras da ficha da BQ que a página Boquilhas fazia ela própria contra
/// o contexto: as mesmas consultas, só leitura (AsNoTracking).
/// </summary>
public sealed class BoquilhasFichaQuery : IBoquilhasFichaQuery
{
    private readonly DmoDbContext _db;

    public BoquilhasFichaQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MachineOption>> MaquinasDaFerramentaAsync(
        string toolId, CancellationToken cancellationToken = default) =>
        await _db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => link.ToolId == toolId)
            .OrderBy(link => link.MachineId)
            .Select(link => new MachineOption(link.MachineId, link.Machine.Code))
            .ToListAsync(cancellationToken);

    public Task<BqFichaFerramenta?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default) =>
        _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new BqFichaFerramenta(t.Reference, t.Lot, t.BqAccountedQuantity))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BqMovimentoSemJobOn>> MovimentosSemJobOnAsync(
        string toolId, int limite, CancellationToken cancellationToken = default) =>
        await _db.BqMovements.AsNoTracking()
            .Where(m => m.Trace.ToolId == toolId && m.Trace.BqContextId == null)
            .OrderByDescending(m => m.OccurredAtUtc).ThenByDescending(m => m.Id)
            .Take(limite)
            .Select(m => new BqMovimentoSemJobOn(m.Type, m.Quantity, m.MachineCodeFrozen, m.OccurredAtUtc))
            .ToListAsync(cancellationToken);
}

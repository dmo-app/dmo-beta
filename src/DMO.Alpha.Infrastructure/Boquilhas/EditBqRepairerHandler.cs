using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A edição do NOME de uma entidade reparadora. A edição NUNCA reescreve
/// os movimentos passados: o nome congelado (BqRepairerNameFrozen) de
/// cada saída é a verdade histórica DELA (blueprint boquilhas/OVERVIEW.md
/// L372-374) — as saídas futuras é que passam a congelar o nome novo.
/// Recusas tipadas: NotFound, EmptyName, NameAlreadyInUse.
/// </summary>
public sealed class EditBqRepairerHandler : IEditBqRepairerHandler
{
    private readonly DmoDbContext _db;

    public EditBqRepairerHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<BqRepairerWriteOutcome> HandleAsync(
        EditBqRepairer command,
        CancellationToken cancellationToken = default)
    {
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.EmptyName);
        }

        var repairer = await _db.BqRepairers
            .FirstOrDefaultAsync(r => r.Id == command.BqRepairerId, cancellationToken);
        if (repairer is null)
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.NotFound);
        }

        if (await _db.BqRepairers.AsNoTracking()
            .AnyAsync(r => r.Name == name && r.Id != command.BqRepairerId, cancellationToken))
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.NameAlreadyInUse);
        }

        repairer.Name = name;
        await _db.SaveChangesAsync(cancellationToken);

        return new BqRepairerWriteOutcome(BqRepairerWriteResult.Success, repairer.Id, repairer.Name);
    }
}

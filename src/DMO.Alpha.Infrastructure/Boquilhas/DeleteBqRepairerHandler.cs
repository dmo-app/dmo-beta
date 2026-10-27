using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A remoção de uma entidade reparadora (Diogo, 21/10/2026), a eliminação
/// canónica que a FK Restrict previa: numa só gravação, as saídas que
/// apontam para ela congelam o nome (as que ainda não o tinham) e largam a
/// FK, as rotas das máquinas que a usavam ficam sem reparador, e a entidade
/// sai. A verdade histórica de cada movimento continua a ser o nome
/// congelado (blueprint boquilhas/OVERVIEW.md L372-374).
/// </summary>
public sealed class DeleteBqRepairerHandler : IDeleteBqRepairerHandler
{
    private readonly DmoDbContext _db;

    public DeleteBqRepairerHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<BqRepairerWriteOutcome> HandleAsync(
        DeleteBqRepairer command,
        CancellationToken cancellationToken = default)
    {
        var repairer = await _db.BqRepairers
            .FirstOrDefaultAsync(r => r.Id == command.BqRepairerId, cancellationToken);
        if (repairer is null)
        {
            return new BqRepairerWriteOutcome(BqRepairerWriteResult.NotFound);
        }

        var movements = await _db.BqMovements
            .Where(m => m.BqRepairerId == repairer.Id)
            .ToListAsync(cancellationToken);
        foreach (var movement in movements)
        {
            movement.BqRepairerNameFrozen ??= repairer.Name;
            movement.BqRepairerId = null;
        }

        // As saídas de BQ do Armazém também apontam para ele (chave estrangeira
        // RESTRICT): largam a ligação e ficam com o nome, que já guardam desde
        // a saída. Sem isto a base recusava e a página dava erro (vistoria
        // BD-02, 27/10/2026).
        var armazem = await _db.ArmazemMovimentos
            .Where(m => m.BqReparadorId == repairer.Id)
            .ToListAsync(cancellationToken);
        foreach (var movimento in armazem)
        {
            movimento.ReparadorNome ??= repairer.Name;
            movimento.BqReparadorId = null;
        }

        var routeKeys =BqRepairerRouting.MachineCodes.Select(BqRepairerRouting.SettingKeyFor).ToList();
        var idText = repairer.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var routes = await _db.ModuleSettings
            .Where(s => routeKeys.Contains(s.SettingKey) && s.SettingValue == idText)
            .ToListAsync(cancellationToken);
        foreach (var route in routes)
        {
            route.SettingValue = string.Empty;
        }

        _db.BqRepairers.Remove(repairer);
        await _db.SaveChangesAsync(cancellationToken);

        return new BqRepairerWriteOutcome(BqRepairerWriteResult.Success, repairer.Id, repairer.Name);
    }
}

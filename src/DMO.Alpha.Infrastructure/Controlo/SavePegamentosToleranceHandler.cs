using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Grava a tolerância do corredor de Pegamentos na linha única da
/// configuração (cria-a na primeira gravação, atualiza-a depois). Os
/// registos já criados mantêm a tolerância congelada.
/// </summary>
public sealed class SavePegamentosToleranceHandler : ISavePegamentosToleranceHandler
{
    private readonly DmoDbContext _db;

    public SavePegamentosToleranceHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<SavePegamentosToleranceResult> SaveAsync(
        decimal toleranceMm,
        CancellationToken cancellationToken = default)
    {
        if (!PegamentosToleranceLimits.IsValid(toleranceMm))
        {
            return SavePegamentosToleranceResult.OutOfRange;
        }

        var existing = await _db.PegamentosTolerances
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            _db.PegamentosTolerances.Add(new PegamentosTolerance { ToleranceMm = toleranceMm });
        }
        else
        {
            existing.ToleranceMm = toleranceMm;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return SavePegamentosToleranceResult.Success;
    }
}

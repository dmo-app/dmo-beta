using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>O resultado do «Remover por engano».</summary>
public enum RemoveJobOnChangeResult
{
    Removed,

    /// <summary>Já estava removida (um segundo envio): nada muda.</summary>
    AlreadyRemoved,

    /// <summary>A alteração não existe (ou não é deste Job On).</summary>
    NotFound
}

/// <summary>
/// REMOVER UMA ALTERAÇÃO POR ENGANO (Diogo, 23/10/2026: «ter forma de
/// remover o notice de troca em caso de engano»). Nada se perde: a linha não
/// se apaga, fica marcada como removida com quem e quando. Os valores do Job
/// On não mudam: só o aviso deixa de contar.
/// </summary>
public sealed class RemoveJobOnChangeHandler(DmoDbContext db)
{
    public async Task<RemoveJobOnChangeResult> HandleAsync(
        int jobOnId, int changeId, string? removedBy, CancellationToken cancellationToken = default)
    {
        var change = await db.JobOnChanges.FirstOrDefaultAsync(c => c.Id == changeId && c.JobOnId == jobOnId, cancellationToken);
        if (change is null)
        {
            return RemoveJobOnChangeResult.NotFound;
        }

        if (change.RemovedAtUtc is not null)
        {
            return RemoveJobOnChangeResult.AlreadyRemoved;
        }

        change.RemovedAtUtc = DateTime.UtcNow;
        var quem = removedBy?.Trim();
        change.RemovedByNameFrozen = string.IsNullOrEmpty(quem) ? null : quem.Length > 254 ? quem[..254] : quem;
        await db.SaveChangesAsync(cancellationToken);
        return RemoveJobOnChangeResult.Removed;
    }
}

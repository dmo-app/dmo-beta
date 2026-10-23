using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>O resultado do «Remover por engano».</summary>
public enum RemoveJobOnChangeResult
{
    Removed,

    /// <summary>A alteração não existe (ou não é deste Job On).</summary>
    NotFound
}

/// <summary>
/// REMOVER UMA ALTERAÇÃO POR ENGANO (Diogo, 23/10/2026: «ter forma de
/// remover o notice de troca em caso de engano»). Os valores do Job
/// On não mudam: só o aviso deixa de contar. Desde 23/10/2026 a alteração
/// apaga-se de vez (Diogo: «remover por completo, senão pode começar a angariar lixo»).
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

        db.JobOnChanges.Remove(change);
        await db.SaveChangesAsync(cancellationToken);
        return RemoveJobOnChangeResult.Removed;
    }
}

using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Gravações que procuram e depois criam («existe? se não, cria») só ficam
/// certas com dois pedidos ao mesmo tempo se o segundo esperar pelo
/// primeiro. <see cref="InTransactionAsync{T}"/> corre a gravação numa
/// transação; dentro dela, <see cref="LockAsync"/> segura um trinco com nome
/// até ao commit, e o segundo pedido espera e já vê o que o primeiro gravou.
/// Sem base relacional (testes em memória), ou dentro de uma transação que já
/// existe, corre tal como está.
/// </summary>
public static class DbWrite
{
    public static async Task<T> InTransactionAsync<T>(
        DbContext db,
        Func<Task<T>> write,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
        {
            return await write();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await write();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Trinco com nome (pg_advisory_xact_lock) até ao fim da transação
    /// corrente; sem base relacional não faz nada.
    /// </summary>
    public static async Task LockAsync(DbContext db, string name, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({name}))", cancellationToken);
    }

    /// <summary>
    /// O mesmo trinco, partilhado: vários pedidos com o partilhado correm ao
    /// mesmo tempo, mas esperam por quem tem o <see cref="LockAsync"/> do
    /// mesmo nome (e este espera por eles). Sem base relacional não faz nada.
    /// </summary>
    public static async Task LockSharedAsync(DbContext db, string name, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock_shared(hashtext({name}))", cancellationToken);
    }
}

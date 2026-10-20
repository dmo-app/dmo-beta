using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Trinco do registo antes de recalcular as discrepâncias. Registar, corrigir
/// e apagar leem todos os movimentos do registo e regravam a discrepância de
/// cada um; dois pedidos ao mesmo tempo liam o mesmo estado e o segundo não
/// via o primeiro (18/10/2026: saída de 10 e duas entradas simultâneas de 10
/// davam discrepância total 0 em vez de −10). Abre uma transação e bloqueia a
/// linha do registo (FOR UPDATE) até ao commit: o segundo pedido espera e lê
/// já o movimento do primeiro. Sem base relacional (testes em memória) não
/// há transação.
/// </summary>
internal static class BqTraceLock
{
    public static async Task<IDbContextTransaction?> AcquireAsync(
        DmoDbContext db,
        int traceId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "BqRepairTraces" WHERE "Id" = {traceId} FOR UPDATE""",
            cancellationToken);
        return transaction;
    }
}

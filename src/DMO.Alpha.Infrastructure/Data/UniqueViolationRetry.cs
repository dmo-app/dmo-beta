using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Dois pedidos ao mesmo tempo (um duplo clique, duas abas, dois chefes)
/// podem tentar criar a mesma linha ou a mesma posição: o índice único
/// recusa o segundo com 23505. Em vez de um erro 500, o segundo pedido
/// corre outra vez, UMA só vez, já com a linha do primeiro à vista (o
/// get-or-create encontra-a; a posição seguinte é recalculada). Uma
/// segunda falha propaga: deixou de ser corrida.
/// O ChangeTracker é limpo antes da segunda tentativa: as entidades
/// acrescentadas na primeira não podem voltar a ser gravadas.
/// No InMemory (dev/testes) os índices únicos não existem e a primeira
/// tentativa é a única.
/// </summary>
public static class UniqueViolationRetry
{
    public static async Task<T> RunAsync<T>(DbContext db, Func<Task<T>> attempt)
    {
        try
        {
            return await attempt();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await attempt();
        }
    }

    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

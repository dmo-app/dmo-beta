using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// O duplo clique conta 10 s desde o registo da ferramenta. Os testes que
/// repetem um registo de propósito (a ferramenta repetida) envelhecem antes
/// as que já existem, como se tivessem sido registadas há mais tempo.
/// </summary>
internal static class RegistosAntigos
{
    public static async Task EnvelhecerAsync(DmoDbContext db)
    {
        foreach (var tool in await db.Tools.ToListAsync())
        {
            tool.RegisteredAtUtc = null;
        }

        await db.SaveChangesAsync();
    }
}

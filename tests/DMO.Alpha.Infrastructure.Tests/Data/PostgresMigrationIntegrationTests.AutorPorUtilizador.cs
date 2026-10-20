using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O autor pelo utilizador (E3 do DMO-FULL) na base: o identificador aponta
/// para Users, e eliminar o utilizador continua a funcionar como na beta. O
/// facto fica, com o texto do momento e sem identificador.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task AutorPorUtilizador_EliminarOUtilizador_OFactoFicaComOTexto()
    {
        SkipWithoutConnection();
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var marca = Guid.NewGuid().ToString("N")[..10];
        var user = new User { Id = Guid.NewGuid(), Name = "Autor " + marca, OperatorId = marca[..4], ProviderUserId = "p-" + marca };
        var toolId = "AUTOR-" + marca;
        await using (var db = CreateContext())
        {
            db.Users.Add(user);
            db.Tools.Add(new Tool
            {
                ToolId = toolId, Type = ToolType.Cm, Reference = "1075", Lot = marca,
                DuplicateWarningByNameFrozen = user.Name, DuplicateWarningByUserId = user.Id
            });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == user.Id));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var tool = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal(user.Name, tool.DuplicateWarningByNameFrozen);
            Assert.Null(tool.DuplicateWarningByUserId);

            db.Tools.Remove(await db.Tools.SingleAsync(t => t.ToolId == toolId));
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task AutorPorUtilizador_UmIdentificadorSemUtilizador_ERecusado()
    {
        SkipWithoutConnection();
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var contexto = CreateContext();
        contexto.Tools.Add(new Tool
        {
            ToolId = "AUTOR-X-" + Guid.NewGuid().ToString("N")[..8], Type = ToolType.Cm, Reference = "1075", Lot = "1",
            DuplicateWarningByNameFrozen = "Ninguém", DuplicateWarningByUserId = Guid.NewGuid()
        });
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        Assert.Equal("23503", (erro.InnerException as Npgsql.PostgresException)?.SqlState); // foreign_key_violation
    }
}

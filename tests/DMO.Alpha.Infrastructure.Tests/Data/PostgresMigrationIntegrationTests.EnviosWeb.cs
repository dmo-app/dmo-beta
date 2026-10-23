using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task EnvioWeb_RepetidoNoutraInstancia_NaoRepeteOFacto()
    {
        SkipWithoutConnection();
        var id = Guid.NewGuid();
        var autor = Guid.NewGuid();
        var code = "EW" + id.ToString("N")[..10];
        await using var first = CreateContext();
        await first.Database.MigrateAsync();
        await using var second = CreateContext();
        try
        {
            async Task<string?> Write(DmoDbContext db)
            {
                db.Machines.Add(new Machine { Code = code });
                await db.SaveChangesAsync();
                await Task.Delay(100);
                return "/JobOn?jobon_id=7";
            }
            var respostas = await Task.WhenAll(
                new EnvioWebPersistido(first).ExecutarAsync(id, autor, "hash-a", () => Write(first), default),
                new EnvioWebPersistido(second).ExecutarAsync(id, autor, "hash-a", () => Write(second), default));
            Assert.All(respostas, r => Assert.Equal("/JobOn?jobon_id=7", r.Resposta));
            await using var leitura = CreateContext();
            Assert.Single(await leitura.Machines.AsNoTracking().Where(m => m.Code == code).ToListAsync());
            var conflitante = await new EnvioWebPersistido(leitura).ExecutarAsync(id, autor, "hash-b",
                () => throw new InvalidOperationException("Não deve repetir"), default);
            Assert.True(conflitante.Conflito);
        }
        finally
        {
            await using var clean = CreateContext();
            await clean.OperacoesRegistadas.Where(o => o.Id == id).ExecuteDeleteAsync();
            await clean.Machines.Where(m => m.Code == code).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task EnvioWeb_SeFalhar_NemOFactoNemARespostaFicamGravados()
    {
        SkipWithoutConnection();
        var id = Guid.NewGuid();
        var code = "EW" + id.ToString("N")[..10];
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EnvioWebPersistido(db).ExecutarAsync(id, null, "hash", async () =>
        {
            db.Machines.Add(new Machine { Code = code });
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Resposta perdida antes do commit");
        }, default));
        await using var leitura = CreateContext();
        Assert.False(await leitura.Machines.AsNoTracking().AnyAsync(m => m.Code == code));
        Assert.False(await leitura.OperacoesRegistadas.AsNoTracking().AnyAsync(o => o.Id == id));
    }
}

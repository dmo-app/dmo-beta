using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Armazem;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O Armazém na base (0.2.0-beta.8, migração ArmazemPosicoesEReparadores): a
/// base recusa um código de posição fora da regra (minúsculas, espaços) e um
/// reparador sem CM nem MF; duas criações da mesma posição ao mesmo tempo
/// deixam uma só, e a segunda lê «já existe».
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Armazem_ABaseRecusaCodigoForaDaRegraEReparadorSemFamilia()
    {
        SkipWithoutConnection();
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        foreach (var codigo in new[] { "7001a", "00 42", "" })
        {
            await using var db = CreateContext();
            db.ArmazemPosicoes.Add(new ArmazemPosicao { Codigo = codigo, CriadaEmUtc = DateTime.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = CreateContext())
        {
            db.Reparadores.Add(new Reparador { Nome = "Sem familia " + Guid.NewGuid().ToString("N")[..8], AplicaCm = false, AplicaMf = false });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [SkippableFact]
    public async Task Armazem_AMesmaPosicaoAoMesmoTempo_FicaUmaSo()
    {
        SkipWithoutConnection();
        string codigo;
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            // Quatro algarismos que a base ainda não tem.
            var usados = (await db.ArmazemPosicoes.Select(p => p.Codigo).ToListAsync()).ToHashSet();
            codigo = Enumerable.Range(1000, 9000).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Where(c => !usados.Contains(c)).OrderBy(_ => Random.Shared.Next()).First();
        }

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var db = CreateContext();
            return (await new ArmazemPosicoes(db, new SemSessao()).CriarAsync(codigo)).Resultado;
        }));

        Assert.Single(resultados, r => r == ArmazemPosicaoResultado.Gravado);
        Assert.All(resultados.Where(r => r != ArmazemPosicaoResultado.Gravado), r => Assert.Equal(ArmazemPosicaoResultado.JaExiste, r));
        await using (var db = CreateContext())
        {
            Assert.Equal(1, await db.ArmazemPosicoes.CountAsync(p => p.Codigo == codigo));
            db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes.Where(p => p.Codigo == codigo));
            await db.SaveChangesAsync();
        }
    }

    private sealed class SemSessao : ICurrentAccountContext
    {
        public ActorContext Current => ActorContext.Anonymous;
    }
}

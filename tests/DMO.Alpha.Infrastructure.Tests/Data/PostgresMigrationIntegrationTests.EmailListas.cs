using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task EmailLista_UmaGravacaoConcorrenteNaoApagaAAlteracaoDeOutraPessoa()
    {
        SkipWithoutConnection();
        await using var seed = CreateContext();
        await seed.Database.MigrateAsync();
        var lista = new EmailLista { Nome = "Concorrência " + Guid.NewGuid(), Destinatarios = "ana@fabrica.pt\nbruno@fabrica.pt" };
        seed.EmailListas.Add(lista);
        await seed.SaveChangesAsync();
        try
        {
            await using var antiga = CreateContext();
            await antiga.EmailListas.SingleAsync(l => l.Id == lista.Id);
            await using var outra = CreateContext();
            var primeira = await new EnvioEmailDefinicoes(outra).MudarPessoaAsync(lista.Id, 0, "Ana Silva", null);
            Assert.True(primeira.Gravado);

            var segunda = await new EnvioEmailDefinicoes(antiga).RemoverPessoaAsync(lista.Id, 1);

            Assert.Equal("Conflito", segunda.Estado.ToString());
            await using var check = CreateContext();
            Assert.Equal("Ana Silva <ana@fabrica.pt>\nbruno@fabrica.pt",
                (await check.EmailListas.AsNoTracking().SingleAsync(l => l.Id == lista.Id)).Destinatarios);
        }
        finally
        {
            await seed.EmailListas.Where(l => l.Id == lista.Id).ExecuteDeleteAsync();
        }
    }
}

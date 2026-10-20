using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O envio único guardado na base (E4 do DMO-FULL): o mesmo envio duas vezes
/// grava uma só vez. O segundo colide na chave primária das operações e o
/// facto dele não fica; o primeiro resultado é o que se relê. Um facto de
/// teste (uma definição de módulo com uma chave só deste teste) faz de
/// registo do módulo. Dois envios diferentes gravam os dois: a recusa é só
/// do repetido.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task OperacaoRegistada_OMesmoEnvioDuasVezes_GravaUmaSoVez()
    {
        SkipWithoutConnection();
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var operacaoId = Guid.NewGuid();
        var marca = "Teste:Operacao" + operacaoId.ToString("N")[..12];
        var autor = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = marca + ":1", SettingValue = "primeiro" });
            new OperacoesRegistadas(db, new Autor(autor)).Registar(operacaoId, "teste.gravar", new ResultadoDaOperacao("Teste", 7));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = marca + ":2", SettingValue = "repetido" });
            new OperacoesRegistadas(db, new Autor(autor)).Registar(operacaoId, "teste.gravar", new ResultadoDaOperacao("Teste", 8));
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(OperacoesRegistadas.EOperacaoRepetida(erro));
        }

        await using (var db = CreateContext())
        {
            var gravados = await db.ModuleSettings.AsNoTracking()
                .Where(s => s.SettingKey.StartsWith(marca))
                .Select(s => s.SettingValue)
                .ToListAsync();
            Assert.Equal(["primeiro"], gravados);

            var operacao = await new OperacoesRegistadas(db).ProcurarAsync(operacaoId);
            Assert.NotNull(operacao);
            Assert.Equal(("Teste", 7, autor), (operacao!.ResultadoTipo, operacao.ResultadoId, operacao.AutorUserId!.Value));
            Assert.Null(await new OperacoesRegistadas(db).ProcurarAsync(Guid.NewGuid()));

            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey.StartsWith(marca)));
            db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.Id == operacaoId));
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task OperacaoRegistada_DoisEnviosDiferentes_GravamOsDois()
    {
        SkipWithoutConnection();
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var primeiro = Guid.NewGuid();
        var segundo = Guid.NewGuid();
        var marca = "Teste:Operacao" + primeiro.ToString("N")[..12];

        foreach (var (operacaoId, n) in new[] { (primeiro, 1), (segundo, 2) })
        {
            await using var db = CreateContext();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = $"{marca}:{n}", SettingValue = $"envio {n}" });
            new OperacoesRegistadas(db).Registar(operacaoId, "teste.gravar", new ResultadoDaOperacao("Teste", n));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var gravados = await db.ModuleSettings.AsNoTracking()
                .Where(s => s.SettingKey.StartsWith(marca))
                .OrderBy(s => s.SettingKey)
                .Select(s => s.SettingValue)
                .ToListAsync();
            Assert.Equal(["envio 1", "envio 2"], gravados);
            Assert.Equal(1, (await new OperacoesRegistadas(db).ProcurarAsync(primeiro))?.ResultadoId);
            Assert.Equal(2, (await new OperacoesRegistadas(db).ProcurarAsync(segundo))?.ResultadoId);

            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey.StartsWith(marca)));
            db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.Id == primeiro || o.Id == segundo));
            await db.SaveChangesAsync();
        }
    }

    private sealed class Autor(Guid id) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = id, DisplayName = "Autor" };
    }
}

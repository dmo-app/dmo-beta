using System.Data.Common;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Vistoria ao backend (27/10/2026, B3): ler o ficheiro do SAP fazia duas
    // consultas por ferramenta (os lembretes abertos e a última leitura). As
    // regras ficam iguais (grava com o lembrete aberto ou a percentagem
    // mudada, e fecha o lembrete), mas as leituras à base já não crescem com
    // o número de ferramentas do ficheiro.
    [SkippableFact]
    public async Task UsoSap_ImportarComAsMesmasConsultasPara5Ou100Ferramentas_NoPostgreSql()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marca = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        try
        {
            var (atualizadas5, consultas5) = await ImportarAsync(setup, marca + "A", 5);
            var (atualizadas100, consultas100) = await ImportarAsync(setup, marca + "B", 100);

            // Dois em cada três têm o lembrete aberto ou outra percentagem;
            // o terceiro já tem a mesma percentagem e fica igual.
            Assert.Equal(Enumerable.Range(0, 5).Count(i => i % 3 != 2), atualizadas5);
            Assert.Equal(Enumerable.Range(0, 100).Count(i => i % 3 != 2), atualizadas100);
            Assert.Equal(consultas5, consultas100);

            await using var leitura = CreateContext();
            var lembretes = await leitura.FerramentaLembretesUtilizacao.AsNoTracking()
                .Where(l => l.ToolId.StartsWith("U" + marca))
                .ToListAsync();
            Assert.NotEmpty(lembretes);
            Assert.All(lembretes, l => Assert.NotNull(l.FechadoEmUtc));
            Assert.All(lembretes, l => Assert.NotNull(l.UtilizacaoId));
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.FerramentaLembretesUtilizacao.Where(l => l.ToolId.StartsWith("U" + marca)).ExecuteDeleteAsync();
            await limpar.FerramentaUtilizacoes.Where(u => u.ToolId.StartsWith("U" + marca)).ExecuteDeleteAsync();
            await limpar.Tools.Where(t => t.ToolId.StartsWith("U" + marca)).ExecuteDeleteAsync();
        }
    }

    private static async Task<(int Atualizadas, int Consultas)> ImportarAsync(DmoDbContext setup, string marca, int quantas)
    {
        var linhas = new List<string>();
        for (var i = 0; i < quantas; i++)
        {
            var nome = $"MZZ{marca}T{i:000}X";
            var toolId = $"U{marca}{i:000}";
            setup.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = $"ZZ{marca}{i:000}", Lot = "7", TechnicalName = nome, RegisteredAtUtc = DateTime.UtcNow });
            switch (i % 3)
            {
                case 0:
                    setup.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = toolId, CriadoEmUtc = DateTime.UtcNow });
                    break;
                case 1:
                    setup.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 10m, Origem = FerramentaUtilizacao.OrigemManual, RegistadaEmUtc = DateTime.UtcNow });
                    break;
                default:
                    setup.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 50.5m, Origem = FerramentaUtilizacao.OrigemManual, RegistadaEmUtc = DateTime.UtcNow });
                    break;
            }

            linhas.Add($"<tr><td>{nome}07</td><td>43.200.000</td><td>21.816.000</td><td>50,5</td></tr>");
        }

        await setup.SaveChangesAsync();
        setup.ChangeTracker.Clear();

        var lista = "<table><tr><th>Equipamento</th><th>Expected life</th><th>Consumption [million units]</th><th>% Use</th></tr>"
            + string.Concat(linhas) + "</table>";
        var contador = new ContaLeituras();
        await using var db = CreateContextWith(contador);
        var outcome = await new FerramentasUsoSap(db, new SessaoUsoSap()).ImportarAsync(lista, "percentagem.HTML", DateTime.UtcNow);
        return (outcome.Atualizadas, contador.Vezes);
    }

    /// <summary>Só as leituras (SELECT); as gravações do lote não contam.</summary>
    private sealed class ContaLeituras : DbCommandInterceptor
    {
        public int Vezes { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                Vezes++;
            }

            return ValueTask.FromResult(result);
        }
    }
}

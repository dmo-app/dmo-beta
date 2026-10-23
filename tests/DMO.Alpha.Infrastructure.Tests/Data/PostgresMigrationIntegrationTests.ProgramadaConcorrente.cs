using System.Data.Common;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Programada;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Programada_AsDuasUltimasFerramentasVoltamAoMesmoTempo_AListaFecha()
    {
        SkipWithoutConnection();
        var cenario = await CriarProgramadaConcorrenteAsync(duasFerramentas: true);
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var primeiraPodeFechar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primeiraGravada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var pausa1 = new ProgramadaDepoisDeContar(primeiraPodeFechar.Task);
            var pausa2 = new ProgramadaDepoisDeContar(primeiraGravada.Task);
            await using var primeira = CreateContextWith(pausa1);
            await using var segunda = CreateContextWith(pausa2);
            await segunda.Database.OpenConnectionAsync(prazo.Token);
            var pid2 = ((NpgsqlConnection)segunda.Database.GetDbConnection()).ProcessID;
            var entrada1 = MarcarEntradaProgramadaAsync(primeira, cenario.Entrada1, prazo.Token);
            await pausa1.Pronta.Task.WaitAsync(prazo.Token);
            var entrada2 = MarcarEntradaProgramadaAsync(segunda, cenario.Entrada2, prazo.Token);
            try
            {
                await AguardarBloqueioOuGravacaoAsync(pid2, pausa2.Pronta.Task, prazo.Token);
                primeiraPodeFechar.TrySetResult();
                await entrada1;
                primeiraGravada.TrySetResult();
                await entrada2;
            }
            finally
            {
                primeiraPodeFechar.TrySetResult();
                primeiraGravada.TrySetResult();
                prazo.Cancel();
                try { await Task.WhenAll(entrada1, entrada2); }
                catch (OperationCanceledException) { }
            }

            await using var check = CreateContext();
            var itens = await check.ReparacaoProgramadaItens.AsNoTracking().Where(i => i.ListaId == cenario.ListaId).ToListAsync();
            Assert.Equal(2, itens.Count);
            Assert.All(itens, i => Assert.NotNull(i.EntradaMovimentoId));
            Assert.NotNull((await check.ReparacaoProgramadaListas.AsNoTracking().SingleAsync(l => l.Id == cenario.ListaId)).FechadaEmUtc);
        }
        finally
        {
            primeiraPodeFechar.TrySetResult();
            primeiraGravada.TrySetResult();
            await LimparProgramadaConcorrenteAsync(cenario);
        }
    }

    [SkippableFact]
    public async Task Programada_AdicionarEnquantoAUltimaFerramentaVolta_NaoAcrescentaAUmaListaFechada()
    {
        SkipWithoutConnection();
        var cenario = await CriarProgramadaConcorrenteAsync(duasFerramentas: false);
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var podeFechar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fechada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prontaParaAssociar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var pausaFecho = new ProgramadaDepoisDeContar(podeFechar.Task);
            await using var entrada = CreateContextWith(pausaFecho);
            await using var associacao = CreateContextWith(new BeforeFirstSave(async () =>
            {
                prontaParaAssociar.TrySetResult();
                await fechada.Task.WaitAsync(prazo.Token);
            }));
            await associacao.Database.OpenConnectionAsync(prazo.Token);
            var pid = ((NpgsqlConnection)associacao.Database.GetDbConnection()).ProcessID;
            var fechar = MarcarEntradaProgramadaAsync(entrada, cenario.Entrada1, prazo.Token);
            await pausaFecho.Pronta.Task.WaitAsync(prazo.Token);
            var adicionar = new ReparacaoProgramadaServico(associacao, new SemSessao())
                .AssociarAsync([cenario.Tool2], cenario.ListaId, null, prazo.Token);
            ProgramadaAssociacaoSaida resultado;
            try
            {
                await AguardarBloqueioOuGravacaoAsync(pid, prontaParaAssociar.Task, prazo.Token);
                podeFechar.TrySetResult();
                await fechar;
                fechada.TrySetResult();
                resultado = await adicionar;
            }
            finally
            {
                podeFechar.TrySetResult();
                fechada.TrySetResult();
                prazo.Cancel();
                try { await Task.WhenAll(fechar, adicionar); }
                catch (OperationCanceledException) { }
            }

            Assert.Equal(ProgramadaAssociacao.ListaFechada, resultado.Resultado);
            await using var check = CreateContext();
            var item = Assert.Single(await check.ReparacaoProgramadaItens.AsNoTracking().Where(i => i.ListaId == cenario.ListaId).ToListAsync());
            Assert.Equal(cenario.Entrada1.ToolId, item.ToolId);
            Assert.NotNull(item.EntradaMovimentoId);
            Assert.NotNull((await check.ReparacaoProgramadaListas.AsNoTracking().SingleAsync(l => l.Id == cenario.ListaId)).FechadaEmUtc);
        }
        finally
        {
            podeFechar.TrySetResult();
            fechada.TrySetResult();
            await LimparProgramadaConcorrenteAsync(cenario);
        }
    }

    private static async Task MarcarEntradaProgramadaAsync(DmoDbContext db, ArmazemMovimento entrada, CancellationToken cancellationToken)
    {
        // A chamada real vem dentro da transação do movimento do Armazém.
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        await ReparacaoProgramadaMovimentos.AoMovimentoAsync(db, entrada, cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private sealed class ProgramadaDepoisDeContar(Task continuar) : DbCommandInterceptor
    {
        public TaskCompletionSource Pronta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            // A consulta das listas com itens ainda por devolver, depois de
            // gravar a entrada. Não intercetar a descoberta inicial das listas.
            if (command.CommandText.Contains("DISTINCT") && command.CommandText.Contains("\"ReparacaoProgramadaItens\"")
                && !command.CommandText.Contains("\"ReparacaoProgramadaListas\""))
            {
                Pronta.TrySetResult();
                await continuar.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed record ProgramadaConcorrente(string Marca, int ListaId, string Tool2, ArmazemMovimento Entrada1, ArmazemMovimento Entrada2);

    private static async Task<ProgramadaConcorrente> CriarProgramadaConcorrenteAsync(bool duasFerramentas)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marca = "RPC-" + Guid.NewGuid().ToString("N");
        var tool1 = marca + "-CM";
        var tool2 = marca + "-MF";
        db.Tools.AddRange(
            new Tool { ToolId = tool1, Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = tool2, Type = ToolType.Mf, Reference = "5447", Lot = "1" });
        var lista = new ReparacaoProgramadaLista { ReparadorNome = marca, CriadaEmUtc = DateTime.UtcNow };
        db.ReparacaoProgramadaListas.Add(lista);
        await db.SaveChangesAsync();
        db.ReparacaoProgramadaItens.Add(new ReparacaoProgramadaItem
        {
            ListaId = lista.Id, ToolId = tool1, Tipo = ToolType.Cm, Referencia = "5447", Lote = "1", AdicionadaEmUtc = DateTime.UtcNow
        });
        if (duasFerramentas)
        {
            db.ReparacaoProgramadaItens.Add(new ReparacaoProgramadaItem
            {
                ListaId = lista.Id, ToolId = tool2, Tipo = ToolType.Mf, Referencia = "5447", Lote = "1", AdicionadaEmUtc = DateTime.UtcNow
            });
        }
        ArmazemMovimento Entrada(string toolId) => new()
        {
            ToolId = toolId, Tipo = ArmazemMovimentoTipo.Entrada, OperacaoId = Guid.NewGuid(),
            OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow
        };
        var entrada1 = Entrada(tool1);
        var entrada2 = Entrada(tool2);
        db.ArmazemMovimentos.AddRange(entrada1, entrada2);
        await db.SaveChangesAsync();
        return new(marca, lista.Id, tool2, entrada1, entrada2);
    }

    private static async Task LimparProgramadaConcorrenteAsync(ProgramadaConcorrente cenario)
    {
        await using var db = CreateContext();
        await db.ReparacaoProgramadaItens.Where(i => i.ListaId == cenario.ListaId).ExecuteDeleteAsync();
        await db.ReparacaoProgramadaListas.Where(l => l.Id == cenario.ListaId).ExecuteDeleteAsync();
        await db.ArmazemMovimentos.Where(m => m.ToolId.StartsWith(cenario.Marca)).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId.StartsWith(cenario.Marca)).ExecuteDeleteAsync();
    }

    // O segundo pedido está à espera de um trinco (pg_locks) ou já gravou.
    private static async Task AguardarBloqueioOuGravacaoAsync(int pid, Task gravacao, CancellationToken cancellationToken)
    {
        await using var observador = CreateContext();
        while (!gravacao.IsCompleted)
        {
            if (await observador.Database.SqlQuery<bool>($"""
                    SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {pid} AND NOT granted) AS "Value"
                    """).SingleAsync(cancellationToken))
            {
                return;
            }
            await Task.Delay(10, cancellationToken);
        }
    }
}

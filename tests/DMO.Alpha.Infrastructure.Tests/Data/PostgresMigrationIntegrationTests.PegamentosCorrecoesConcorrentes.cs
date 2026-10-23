using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pegamentos_DuasCorrecoesDaMesmaLeitura_OHistoricoContinuaDeUmaParaAOutra(bool pelaProducao)
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao);
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var primeiraPodeGravar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primeiraGravada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            int leituraId;
            await using (var setup = CreateContext())
            {
                var leitura = new PegamentosMeasurement
                {
                    PegamentosId = folha.PegamentosId, RowPosition = 1, Component = PegamentosComponent.Cm,
                    CosturaMm = 70.10m, NominalGargaloMm = 70m
                };
                setup.PegamentosMeasurements.Add(leitura);
                await setup.SaveChangesAsync();
                leituraId = leitura.Id;
            }

            var primeiraPausa = new CorrecaoAntesDeGravar(primeiraPodeGravar.Task);
            var segundaPausa = new CorrecaoAntesDeGravar(primeiraGravada.Task);
            await using var primeira = CreateContextWith(primeiraPausa);
            await using var segunda = CreateContextWith(segundaPausa);
            await segunda.Database.OpenConnectionAsync(prazo.Token);
            var segundoPid = ((NpgsqlConnection)segunda.Database.GetDbConnection()).ProcessID;
            var ancora = new PegamentosMeasurementAnchor(leituraId, folha.ControloId,
                pelaProducao ? null : folha.PegamentosId);

            var editarPrimeira = new CorrectPegamentosMeasurementHandler(primeira)
                .EditAsync(new(ancora, 70.20m, null), prazo.Token);
            await primeiraPausa.Pronta.Task.WaitAsync(prazo.Token);
            var editarSegunda = new CorrectPegamentosMeasurementHandler(segunda)
                .EditAsync(new(ancora, 70.30m, null), prazo.Token);
            try
            {
                // Sem a proteção, a segunda já leu o valor antigo e chega ao
                // SaveChanges. Com ela, espera realmente num trinco PostgreSQL.
                await AguardarBloqueioOuGravacaoAsync(segundoPid, segundaPausa.Pronta.Task, prazo.Token);
                primeiraPodeGravar.TrySetResult();
                Assert.Equal(CorrectPegamentosMeasurementResult.Success, await editarPrimeira);
                primeiraGravada.TrySetResult();
                Assert.Equal(CorrectPegamentosMeasurementResult.Success, await editarSegunda);
            }
            finally
            {
                primeiraPodeGravar.TrySetResult();
                primeiraGravada.TrySetResult();
                prazo.Cancel();
                // Não deixar operações vivas a usar os contextos no cleanup.
                try { await Task.WhenAll(editarPrimeira, editarSegunda); }
                catch (OperationCanceledException) { }
            }

            await using var check = CreateContext();
            var rasto = await check.PegamentosMeasurementHistorico.AsNoTracking()
                .Where(h => h.MeasurementId == leituraId).OrderBy(h => h.Id).ToListAsync();
            Assert.Equal(2, rasto.Count);
            Assert.Equal((70.10m, (decimal?)70.20m), (rasto[0].CosturaAntesMm, rasto[0].CosturaDepoisMm));
            Assert.Equal((70.20m, (decimal?)70.30m), (rasto[1].CosturaAntesMm, rasto[1].CosturaDepoisMm));
            Assert.All(rasto, h => Assert.Equal(1, h.Numero));
            Assert.True(rasto[1].EmUtc >= rasto[0].EmUtc);
            Assert.Equal(70.30m, (await check.PegamentosMeasurements.AsNoTracking().SingleAsync(m => m.Id == leituraId)).CosturaMm);
            Assert.Equal(rasto[1].EmUtc, (await check.Pegamentos.AsNoTracking().SingleAsync(p => p.Id == folha.PegamentosId)).CorrigidoEmUtc);
        }
        finally
        {
            primeiraPodeGravar.TrySetResult();
            primeiraGravada.TrySetResult();
            await DeletePegamentosFolhaAsync(folha);
        }
    }

    private sealed class CorrecaoAntesDeGravar(Task continuar) : SaveChangesInterceptor
    {
        public TaskCompletionSource Pronta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PegamentosMeasurementHistorico>().Any(e => e.State == EntityState.Added))
            {
                Pronta.TrySetResult();
                await continuar.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

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

using System.Data.Common;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A05 (relatório de 23/10/2026): duas correções da mesma leitura ao mesmo
/// tempo gravavam 10→11 e 10→12 no histórico, e a cadeia partia-se. Com o
/// trinco da folha, a segunda lê o que a primeira gravou: 10→11 e 11→12.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Pegamentos_DuasCorrecoesAoMesmoTempo_OHistoricoFicaEmCadeia()
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao: false);
        try
        {
            int leituraId;
            await using (var db = CreateContext())
            {
                var leitura = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(
                    new RecordPegamentosMeasurement(0, "CM", 70.10m, PegamentosId: folha.PegamentosId));
                Assert.Equal(RecordPegamentosMeasurementResult.Success, leitura.Result);
                leituraId = leitura.Measurement!.MeasurementId;
            }

            var anchor = new PegamentosMeasurementAnchor(leituraId, null, folha.PegamentosId);
            var primeiraLeu = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var segundaAcabou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<CorrectPegamentosMeasurementResult> PrimeiraAsync()
            {
                await using var db = CreateContextWith(new DepoisDeLerALeitura(primeiraLeu, segundaAcabou.Task));
                return await new CorrectPegamentosMeasurementHandler(db).EditAsync(new EditPegamentosMeasurement(anchor, 70.20m, null));
            }

            async Task<CorrectPegamentosMeasurementResult> SegundaAsync()
            {
                await primeiraLeu.Task.WaitAsync(TimeSpan.FromSeconds(10));
                try
                {
                    await using var db = CreateContext();
                    return await new CorrectPegamentosMeasurementHandler(db).EditAsync(new EditPegamentosMeasurement(anchor, 70.30m, null));
                }
                finally
                {
                    segundaAcabou.TrySetResult();
                }
            }

            var primeira = PrimeiraAsync();
            var segunda = SegundaAsync();
            Assert.Equal(CorrectPegamentosMeasurementResult.Success, await primeira);
            Assert.Equal(CorrectPegamentosMeasurementResult.Success, await segunda);

            await using (var db = CreateContext())
            {
                var historico = await db.PegamentosMeasurementHistorico.AsNoTracking()
                    .Where(h => h.MeasurementId == leituraId)
                    .OrderBy(h => h.Id)
                    .Select(h => new { h.CosturaAntesMm, h.CosturaDepoisMm })
                    .ToListAsync();
                Assert.Equal(2, historico.Count);
                Assert.Equal(70.10m, historico[0].CosturaAntesMm);
                Assert.Equal(historico[0].CosturaDepoisMm, historico[1].CosturaAntesMm);
                var final = await db.PegamentosMeasurements.AsNoTracking().Where(m => m.Id == leituraId).Select(m => m.CosturaMm).SingleAsync();
                Assert.Equal(historico[1].CosturaDepoisMm, final);
            }
        }
        finally
        {
            await using (var db = CreateContext())
            {
                await db.PegamentosMeasurementHistorico.Where(h => h.PegamentosId == folha.PegamentosId).ExecuteDeleteAsync();
            }

            await DeletePegamentosFolhaAsync(folha);
        }
    }

    /// <summary>
    /// Pára a primeira correção logo depois de ler a leitura (a consulta com a
    /// folha), até a segunda acabar ou até ao prazo: com o trinco, a segunda
    /// espera e não acaba antes.
    /// </summary>
    private sealed class DepoisDeLerALeitura(TaskCompletionSource leu, Task outraAcabou) : DbCommandInterceptor
    {
        private int _vezes;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"PegamentosMeasurements\"")
                && command.CommandText.Contains("JOIN \"Pegamentos\"", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Increment(ref _vezes) == 1)
            {
                leu.TrySetResult();
                try { await outraAcabou.WaitAsync(PegamentosApagarPrazo, cancellationToken); }
                catch (TimeoutException) { }
            }

            return result;
        }
    }
}

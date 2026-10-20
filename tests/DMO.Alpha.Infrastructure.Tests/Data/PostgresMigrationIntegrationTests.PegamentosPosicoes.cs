using System.Data.Common;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Deep sweep (18/10/2026, B-pegamentos-1): registar uma leitura e apagar
/// outra da mesma folha ao mesmo tempo (dois separadores) deixava um buraco
/// nas posições (1..10,12). A posição nova era a contagem + 1, por isso a
/// leitura seguinte caía sempre numa posição que já existia: 23505 em
/// IX_PegamentosMeasurements_PegamentosId_RowPosition, erro 500, e a folha
/// ficava sem aceitar leituras até alguém apagar uma.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    // O registo fica parado depois de ler a posição, até o apagar acabar (sem
    // trinco comum, acaba logo) ou até este prazo (com o trinco, o apagar
    // espera pelo registo e não acaba antes).
    private static readonly TimeSpan PegamentosApagarPrazo = TimeSpan.FromSeconds(1.5);

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pegamentos_RegistarEApagarAoMesmoTempo_NaoDeixaBuracoNasPosicoes(bool pelaProducao)
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao);
        try
        {
            var command = new RecordPegamentosMeasurement(
                folha.ControloId ?? 0, "CM", 70.30m, PegamentosId: folha.ControloId is null ? folha.PegamentosId : null);
            for (var i = 0; i < 3; i++)
            {
                await using var db = CreateContext();
                var leitura = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(command);
                Assert.Equal(RecordPegamentosMeasurementResult.Success, leitura.Result);
            }

            int apagada;
            await using (var db = CreateContext())
            {
                apagada = await db.PegamentosMeasurements.AsNoTracking()
                    .Where(m => m.PegamentosId == folha.PegamentosId && m.RowPosition == 2)
                    .Select(m => m.Id).SingleAsync();
            }

            var posicaoLida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var apagou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<RecordPegamentosMeasurementOutcome> RegistarAsync()
            {
                await using var db = CreateContextWith(new DepoisDaPosicao(posicaoLida, apagou.Task));
                return await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(command);
            }

            async Task<CorrectPegamentosMeasurementResult> ApagarAsync()
            {
                await posicaoLida.Task.WaitAsync(TimeSpan.FromSeconds(10));
                try
                {
                    await using var db = CreateContext();
                    return await new CorrectPegamentosMeasurementHandler(db).DeleteAsync(
                        new PegamentosMeasurementAnchor(apagada, folha.ControloId, folha.ControloId is null ? folha.PegamentosId : null));
                }
                finally
                {
                    apagou.TrySetResult();
                }
            }

            var registo = RegistarAsync();
            var apagar = ApagarAsync();
            Assert.Equal(RecordPegamentosMeasurementResult.Success, (await registo).Result);
            Assert.Equal(CorrectPegamentosMeasurementResult.Success, await apagar);
            Assert.Equal(new[] { 1, 2, 3 }, await PosicoesAsync(folha.PegamentosId));

            // A leitura seguinte grava, a seguir às outras.
            await using (var db = CreateContext())
            {
                var seguinte = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(command);
                Assert.Equal(RecordPegamentosMeasurementResult.Success, seguinte.Result);
                Assert.Equal(4, seguinte.Measurement!.RowPosition);
            }
        }
        finally
        {
            await DeletePegamentosFolhaAsync(folha);
        }
    }

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pegamentos_ComUmBuracoJaGravadoNasPosicoes_UmaLeituraNovaGrava(bool pelaProducao)
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao);
        try
        {
            // As folhas que ficaram com um buraco antes da correção (1, 2, 4).
            await using (var db = CreateContext())
            {
                foreach (var posicao in new[] { 1, 2, 4 })
                {
                    db.PegamentosMeasurements.Add(new PegamentosMeasurement
                    {
                        PegamentosId = folha.PegamentosId, RowPosition = posicao, Component = PegamentosComponent.Cm,
                        CosturaMm = 70.10m, NominalGargaloMm = 70.00m
                    });
                }

                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                var leitura = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(
                    new RecordPegamentosMeasurement(folha.ControloId ?? 0, "CM", 70.30m,
                        PegamentosId: folha.ControloId is null ? folha.PegamentosId : null));
                Assert.Equal(RecordPegamentosMeasurementResult.Success, leitura.Result);
            }

            Assert.Equal(new[] { 1, 2, 4, 5 }, await PosicoesAsync(folha.PegamentosId));
        }
        finally
        {
            await DeletePegamentosFolhaAsync(folha);
        }
    }

    private sealed record PegamentosFolha(string Marker, int PegamentosId, int? ControloId, int? MachineId);

    /// <summary>
    /// Uma folha com o CM 70,00 de gargalo: pela produção (controlo_id, a folha
    /// nasce na primeira leitura) ou sem Job On (pegamentos_id).
    /// </summary>
    private async Task<PegamentosFolha> SeedPegamentosFolhaAsync(bool pelaProducao)
    {
        var marker = "PP-" + Guid.NewGuid().ToString("N")[..8];
        var (cm, bq, mf) = (marker + "-CM", marker + "-BQ", marker + "-MF");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "5447B", Lot = "1" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = cm, DiametroGargaloMm = 70.00m });
        await db.SaveChangesAsync();

        if (!pelaProducao)
        {
            var semJobOn = await new CreateToolScopedPegamentosHandler(db, new PegamentosToleranceConfiguration(db))
                .HandleAsync(new CreateToolScopedPegamentos(cm, bq, mf));
            Assert.Equal(CreateToolScopedPegamentosResult.Success, semJobOn.Result);
            return new PegamentosFolha(marker, semJobOn.PegamentosId!.Value, null, null);
        }

        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var created = await new DMO.Alpha.Infrastructure.Production.CreateJobOnHandler(db).HandleAsync(
            new CreateJobOn(marker, "5810C142", machine.Id, cm, mf));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        db.ChangeTracker.Clear();
        var controloId = await db.Controlos.AsNoTracking()
            .Where(c => c.JobOnId == created.JobOn!.JobOnId).Select(c => c.Id).SingleAsync();
        var primeira = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db))
            .HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.10m));
        Assert.Equal(RecordPegamentosMeasurementResult.Success, primeira.Result);
        var pegamentosId = primeira.Measurement!.PegamentosId;
        await db.PegamentosMeasurements.Where(m => m.PegamentosId == pegamentosId).ExecuteDeleteAsync();
        return new PegamentosFolha(marker, pegamentosId, controloId, machine.Id);
    }

    private async Task<int[]> PosicoesAsync(int pegamentosId)
    {
        await using var db = CreateContext();
        return await db.PegamentosMeasurements.AsNoTracking()
            .Where(m => m.PegamentosId == pegamentosId)
            .OrderBy(m => m.RowPosition)
            .Select(m => m.RowPosition)
            .ToArrayAsync();
    }

    private async Task DeletePegamentosFolhaAsync(PegamentosFolha folha)
    {
        await using var db = CreateContext();
        await db.PegamentosMeasurements.Where(m => m.PegamentosId == folha.PegamentosId).ExecuteDeleteAsync();
        await db.Pegamentos.Where(p => p.Id == folha.PegamentosId).ExecuteDeleteAsync();
        if (folha.MachineId is int machineId)
        {
            var jobOns = db.JobOns.Where(j => j.MachineId == machineId).Select(j => j.Id);
            await db.CmContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.MfContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await DeleteJobOnAsync(db, machineId);
        }

        await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(folha.Marker)).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId.StartsWith(folha.Marker)).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Pára o registo logo depois de ele ler a posição da leitura nova (a
    /// consulta às medições da folha), até o apagar acabar ou até ao prazo.
    /// </summary>
    private sealed class DepoisDaPosicao(TaskCompletionSource posicaoLida, Task apagou) : DbCommandInterceptor
    {
        private int _vezes;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"PegamentosMeasurements\"")
                && (command.CommandText.Contains("count(", StringComparison.OrdinalIgnoreCase)
                    || command.CommandText.Contains("max(", StringComparison.OrdinalIgnoreCase))
                && Interlocked.Increment(ref _vezes) == 1)
            {
                posicaoLida.TrySetResult();
                try { await apagou.WaitAsync(PegamentosApagarPrazo, cancellationToken); }
                catch (TimeoutException) { }
            }

            return result;
        }
    }
}

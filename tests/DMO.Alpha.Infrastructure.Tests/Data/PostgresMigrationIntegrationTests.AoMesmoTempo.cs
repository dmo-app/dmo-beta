using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Duas pessoas a gravar ao mesmo tempo, no PostgreSQL: a outra pessoa grava
/// DEPOIS de o pedido ler e ANTES de ele escrever. Na ficha da ferramenta e no
/// Job On quem chega depois é recusado e fica o que a outra pessoa gravou; nos
/// movimentos de BQ o trinco do registo faz o segundo pedido esperar, e a
/// discrepância fica certa com os dois movimentos.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task AoMesmoTempo_Ferramenta_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaOQueElaGravou()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "AMT-" + Guid.NewGuid().ToString("N")[..8];
        var toolId = marker + "-CM";
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            var aberta = (await new ToolSheetQuery(db).FindByToolIdAsync(toolId))!.Version;
            await using var minha = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var outra = CreateContext();
                await outra.Database.ExecuteSqlAsync($"UPDATE \"Tools\" SET \"Lot\" = '9' WHERE \"ToolId\" = {toolId}");
            }));

            var outcome = await SemErroAsync(() => new EditToolFactsHandler(minha).HandleAsync(
                new EditToolFacts(toolId, "CM", marker, "2", ExpectedVersion: aberta)));

            Assert.Equal(EditToolFactsResult.ChangedByAnotherUser, outcome.Result);
            await using var leitura = CreateContext();
            Assert.Equal("9", (await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).Lot);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"ToolMachineLinks\" WHERE \"ToolId\" = {toolId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {toolId}");
        }
    }

    [SkippableFact]
    public async Task AoMesmoTempo_InformacaoTecnica_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaOQueElaGravou()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "AMI-" + Guid.NewGuid().ToString("N")[..8];
        var toolId = marker + "-CM";
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            Assert.Equal(EditToolTechnicalValuesResult.Success, (await new EditToolTechnicalValuesHandler(db).HandleAsync(
                new EditToolTechnicalValues(toolId, NominalWeightGrams: 100m))).Result);
            db.ChangeTracker.Clear();
            var aberta = (await new ToolSheetQuery(db).FindByToolIdAsync(toolId))!.TechnicalVersion;
            Assert.NotEqual(0u, aberta);

            await using var minha = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var outra = CreateContext();
                await outra.Database.ExecuteSqlAsync($"UPDATE \"ToolTechnicalContexts\" SET \"NominalWeightGrams\" = 999 WHERE \"ToolId\" = {toolId}");
            }));

            var outcome = await SemErroAsync(() => new EditToolTechnicalValuesHandler(minha).HandleAsync(
                new EditToolTechnicalValues(toolId, NominalWeightGrams: 250m, ExpectedVersion: aberta)));

            Assert.Equal(EditToolTechnicalValuesResult.ChangedByAnotherUser, outcome.Result);
            await using var leitura = CreateContext();
            Assert.Equal(999m, (await leitura.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).NominalWeightGrams);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"ToolTechnicalContexts\" WHERE \"ToolId\" = {toolId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {toolId}");
        }
    }

    [SkippableFact]
    public async Task AoMesmoTempo_JobOn_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaADataDelaSemLinhaEmOQueMudou()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "AMJ-" + Guid.NewGuid().ToString("N")[..8];
        var mf = marker + "-MF";
        db.Tools.Add(new Tool { ToolId = mf, Type = ToolType.Mf, Reference = marker, Lot = "1" });
        await db.SaveChangesAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        try
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"MfContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOnId}, {mf})");
            var aberta = (await new ProductionContextQuery(db).GetAsync(jobOnId))!.Version;

            await using var minha = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var outra = CreateContext();
                await outra.Database.ExecuteSqlAsync($"UPDATE \"JobOns\" SET \"FimEm\" = DATE '2026-03-20' WHERE \"Id\" = {jobOnId}");
            }));

            var outcome = await SemErroAsync(() => new UpdateJobOnHandler(minha).HandleAsync(new UpdateJobOn(
                jobOnId, machineId, MfToolId: mf, FimEm: new DateOnly(2026, 4, 1), ExpectedVersion: aberta)));

            Assert.Equal(UpdateJobOnResult.ChangedByAnotherUser, outcome.Result);
            await using var leitura = CreateContext();
            Assert.Equal(new DateOnly(2026, 3, 20), (await leitura.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId)).FimEm);
            Assert.Equal(0, await leitura.JobOnChanges.AsNoTracking().CountAsync(c => c.JobOnId == jobOnId));
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" = {jobOnId}");
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"MfContexts\" WHERE \"JobOnId\" = {jobOnId} OR \"ReplacedJobOnId\" = {jobOnId}");
            await DeleteJobOnAsync(db, machineId);
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {mf}");
        }
    }

    [SkippableFact]
    public async Task AoMesmoTempo_BqCorrigirUmaSaidaEnquantoOutroRegistaUmaEntrada_ADiscrepanciaContaComAsDuas()
    {
        // Saída de 10; uma pessoa corrige-a para 20 enquanto outra regista uma
        // entrada de 15. Com as duas gravações, as 15 voltaram de 20 fora:
        // discrepância 0 (sem o trinco ficava −5, calculada com a saída de 10).
        SkipWithoutConnection();

        var (toolId, traceId) = await SeedBqTraceAsync("AMC");
        try
        {
            var saida = await AddBqMovementAsync(traceId, BqMovementType.Saida, 10, BqAoMesmoTempoInicio);

            var corrigido = await ComOutraPessoaARegistarAMeioAsync(
                traceId, 15, BqAoMesmoTempoInicio.AddHours(1),
                minha => new EditBqMovementHandler(minha).HandleAsync(
                    new EditBqMovement(traceId, saida, "saida", 20)));

            Assert.Equal(EditBqMovementResult.Success, corrigido.Result);
            await using var leitura = CreateContext();
            var movimentos = await leitura.BqMovements.AsNoTracking()
                .Where(m => m.BqRepairTraceId == traceId)
                .OrderBy(m => m.OccurredAtUtc)
                .Select(m => new { m.Type, m.Quantity, m.Discrepancy })
                .ToListAsync();
            Assert.Equal(2, movimentos.Count);
            Assert.Equal((BqMovementType.Saida, 20), (movimentos[0].Type, movimentos[0].Quantity));
            Assert.Equal((BqMovementType.Entrada, 15), (movimentos[1].Type, movimentos[1].Quantity));
            Assert.Null(movimentos[1].Discrepancy);
        }
        finally
        {
            await DeleteBqTraceAsync(toolId, traceId);
        }
    }

    [SkippableFact]
    public async Task AoMesmoTempo_BqApagarUmaSaidaEnquantoOutroRegistaUmaEntrada_ADiscrepanciaContaComAsDuas()
    {
        // Saídas de 10 e de 5; uma pessoa apaga a de 5 enquanto outra regista
        // uma entrada de 15. Com as duas gravações, só 10 estavam fora:
        // discrepância −5 (sem o trinco ficava 0, calculada com as duas saídas).
        SkipWithoutConnection();

        var (toolId, traceId) = await SeedBqTraceAsync("AMA");
        try
        {
            await AddBqMovementAsync(traceId, BqMovementType.Saida, 10, BqAoMesmoTempoInicio);
            var segunda = await AddBqMovementAsync(traceId, BqMovementType.Saida, 5, BqAoMesmoTempoInicio.AddMinutes(30));

            var apagado = await ComOutraPessoaARegistarAMeioAsync(
                traceId, 15, BqAoMesmoTempoInicio.AddHours(1),
                minha => new DeleteBqMovementHandler(minha).HandleAsync(new DeleteBqMovement(traceId, segunda)));

            Assert.Equal(DeleteBqMovementResult.Success, apagado);
            await using var leitura = CreateContext();
            var movimentos = await leitura.BqMovements.AsNoTracking()
                .Where(m => m.BqRepairTraceId == traceId)
                .OrderBy(m => m.OccurredAtUtc)
                .Select(m => new { m.Type, m.Quantity, m.Discrepancy })
                .ToListAsync();
            Assert.Equal(2, movimentos.Count);
            Assert.Equal((BqMovementType.Saida, 10), (movimentos[0].Type, movimentos[0].Quantity));
            Assert.Equal((BqMovementType.Entrada, 15), (movimentos[1].Type, movimentos[1].Quantity));
            Assert.Equal(-5, movimentos[1].Discrepancy);
        }
        finally
        {
            await DeleteBqTraceAsync(toolId, traceId);
        }
    }

    /// <summary>A pessoa recebe a recusa como resposta, nunca um erro.</summary>
    private static async Task<T> SemErroAsync<T>(Func<Task<T>> pedido)
    {
        T resultado = default!;
        var erro = await Record.ExceptionAsync(async () => resultado = await pedido());
        Assert.Null(erro);
        return resultado;
    }

    private static readonly DateTime BqAoMesmoTempoInicio = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Corre o pedido desta pessoa; quando ele vai gravar, outra pessoa regista
    /// uma entrada no mesmo registo. Espera que essa gravação acabe ou fique
    /// à espera do trinco, e só então deixa esta pessoa gravar.
    /// </summary>
    private async Task<T> ComOutraPessoaARegistarAMeioAsync<T>(
        int traceId, int entrada, DateTime occurredAtUtc, Func<DmoDbContext, Task<T>> pedido)
    {
        Task<RegisterBqMovementOutcome>? outra = null;
        await using var minha = CreateContextWith(new BeforeFirstSave(async () =>
        {
            outra = Task.Run(async () =>
            {
                await using var ctx = CreateContext();
                return await new RegisterBqMovementHandler(ctx, new NoRouteSettings()).HandleAsync(
                    new RegisterBqMovement(traceId, "entrada", entrada, null, occurredAtUtc, null));
            });
            await AteAcabarOuFicarAEsperarDoTrincoAsync(outra);
        }));

        var resultado = await pedido(minha);
        Assert.NotNull(outra);
        Assert.Equal(RegisterBqMovementResult.Success, (await outra).Result);
        return resultado;
    }

    private async Task AteAcabarOuFicarAEsperarDoTrincoAsync(Task outra)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!outra.IsCompleted && DateTime.UtcNow < limite)
        {
            await using var espia = CreateContext();
            var aEsperar = (await espia.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FOR UPDATE%'")
                .ToListAsync()).Single();
            if (aEsperar > 0)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private async Task<(string ToolId, int TraceId)> SeedBqTraceAsync(string prefix)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var toolId = $"{prefix}-BQ-{Guid.NewGuid().ToString("N")[..8]}";
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
        var trace = new BqRepairTrace { ToolId = toolId, ToolReferenceFrozen = "9389T194", ToolLotFrozen = "1" };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        return (toolId, trace.Id);
    }

    private async Task<int> AddBqMovementAsync(int traceId, BqMovementType type, int quantity, DateTime occurredAtUtc)
    {
        await using var db = CreateContext();
        var movement = new BqMovement
        {
            BqRepairTraceId = traceId, Type = type, Quantity = quantity,
            OccurredAtUtc = occurredAtUtc, CreatedAtUtc = occurredAtUtc
        };
        db.BqMovements.Add(movement);
        await db.SaveChangesAsync();
        return movement.Id;
    }

    private async Task DeleteBqTraceAsync(string toolId, int traceId)
    {
        await using var db = CreateContext();
        await db.BqMovementDeletions.Where(d => d.BqRepairTraceId == traceId).ExecuteDeleteAsync();
        await db.BqMovements.Where(m => m.BqRepairTraceId == traceId).ExecuteDeleteAsync();
        await db.BqRepairTraces.Where(t => t.Id == traceId).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
    }
}

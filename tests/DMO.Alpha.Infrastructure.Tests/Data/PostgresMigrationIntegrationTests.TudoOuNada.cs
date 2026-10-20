using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Gravar tudo ou nada, num PostgreSQL real: quando uma gravação com várias
/// partes falha a meio, nenhuma parte fica (apagar uma ferramenta, aprovar um
/// controlo, editar um Job On). E trocar a BQ de um Job On deixa o registo e
/// os movimentos na BQ em que foram registados.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    /// <summary>Falha de propósito antes da gravação que o teste escolhe.</summary>
    private sealed class FalhaAntesDaGravacao(Func<DbContextEventData, int, bool> falha) : SaveChangesInterceptor
    {
        private int _n;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _n++;
            if (falha(eventData, _n))
            {
                throw new InvalidOperationException("falha de propósito antes da gravação");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Falha de propósito logo DEPOIS de a primeira gravação ter corrido na
    /// base (o SaveChanges já mandou as escritas; o pedido falha a seguir).
    /// </summary>
    private sealed class FalhaDepoisDaPrimeiraGravacao : SaveChangesInterceptor
    {
        private bool _done;

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                throw new InvalidOperationException("falha de propósito depois da gravação");
            }

            return ValueTask.FromResult(result);
        }
    }

    [SkippableFact]
    public async Task ApagarUmaFerramenta_SeAUltimaGravacaoFalhar_AFerramentaFicaEAProducaoContinuaComEla()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TN-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ");

            // A última gravação (a que apaga a ferramenta) falha: o que passou
            // para REMOVED antes, no mesmo pedido, não pode ficar.
            await using var falha = CreateContextWith(new FalhaAntesDaGravacao((e, _) =>
                e.Context!.ChangeTracker.Entries<Tool>().Any(t => t.State == EntityState.Deleted)));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new DeleteToolHandler(falha).HandleAsync(new DeleteTool(marker + "-CM")));

            await using var leitura = CreateContext();
            Assert.True(await leitura.Tools.AnyAsync(t => t.ToolId == marker + "-CM"));
            Assert.Equal(
                marker + "-CM",
                await leitura.CmContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.ToolId).SingleAsync());
            Assert.Equal(
                marker + "-CM",
                await leitura.JobOnLotNotes.Where(n => n.JobOnId == jobOnId).Select(n => n.NewToolId).SingleAsync());
            Assert.False(await leitura.CmContexts.AnyAsync(
                c => c.JobOnId == jobOnId && c.ToolId == ToolRegistryConstants.RemovedToolId));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task Aprovar_SeOResumoDoControloFalharAGravar_NenhumaDecisaoFicaEOControloContinuaPorAprovar()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var (machineId, jobOnId) = await SeedJobOnAsync(setup, "TA-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var controloId = (await setup.Database
                .SqlQuery<int>($"INSERT INTO \"Controlos\" (\"JobOnId\") VALUES ({jobOnId}) RETURNING \"Id\" AS \"Value\"")
                .ToListAsync()).Single();

            // As decisões gravam-se primeiro; o resumo nas colunas do controlo
            // é a segunda gravação, e é essa que falha.
            await using var db = CreateContextWith(new FalhaAntesDaGravacao((_, n) => n == 2));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new ControloApprovalHandler(db).ApproveAndGetAsync(controloId, "1001", "Ok."));

            await using var leitura = CreateContext();
            Assert.Equal(0, await leitura.ControloDecisoes.CountAsync(d => d.ControloId == controloId));
            var controlo = await leitura.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Null(controlo.ApprovedByOperatorId);
            Assert.Null(controlo.ApprovedAtUtc);
        }
        finally
        {
            await DeleteJobOnAsync(setup, machineId);
        }
    }

    [SkippableFact]
    public async Task EditarOJobOn_SeFalharDepoisDeGravar_OJobOnEAsFerramentasFicamComoEstavam()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TJ-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM1", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-CM2", Type = ToolType.Cm, Reference = "5447", Lot = "4" },
            new Tool { ToolId = marker + "-MF1", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-MF2", Type = ToolType.Mf, Reference = "5810", Lot = "2" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T194", Lot = "2" });
        await db.SaveChangesAsync();
        try
        {
            var jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM1", marker + "-MF1", marker + "-BQ1");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();
            var cmId = await db.CmContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            var mfId = await db.MfContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            var bqId = await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();

            // A troca da CM, do MF e da BQ e o fim novo gravam-se; o pedido
            // falha logo a seguir, antes de acabar.
            await using var falha = CreateContextWith(new FalhaDepoisDaPrimeiraGravacao());
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UpdateJobOnHandler(falha).HandleAsync(
                new UpdateJobOn(
                    jobOnId, machineId, marker + "-CM2", marker + "-MF2", marker + "-BQ2",
                    FimEm: new DateOnly(2026, 11, 30))));

            await using var leitura = CreateContext();
            Assert.Null((await leitura.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId)).FimEm);
            Assert.Equal(0, await leitura.JobOnChanges.CountAsync(c => c.JobOnId == jobOnId));
            Assert.Equal(
                new[] { (cmId, marker + "-CM1", (int?)jobOnId) },
                (await leitura.CmContexts.Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                    .Select(c => new { c.Id, c.ToolId, c.JobOnId }).ToListAsync())
                    .Select(c => (c.Id, c.ToolId, c.JobOnId)));
            Assert.Equal(
                new[] { (mfId, marker + "-MF1", (int?)jobOnId) },
                (await leitura.MfContexts.Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                    .Select(c => new { c.Id, c.ToolId, c.JobOnId }).ToListAsync())
                    .Select(c => (c.Id, c.ToolId, c.JobOnId)));
            Assert.Equal(
                new[] { (bqId, marker + "-BQ1", (int?)jobOnId) },
                (await leitura.BqContexts.Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                    .Select(c => new { c.Id, c.ToolId, c.JobOnId }).ToListAsync())
                    .Select(c => (c.Id, c.ToolId, c.JobOnId)));
        }
        finally
        {
            await DeleteProductionAsync(db, marker);
        }
    }

    [SkippableFact]
    public async Task TrocarABq_OsMovimentosEORegistoFicamNaBqEmQueForamRegistados()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "TM-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = marker + "-BQ2", Type = ToolType.Bq, Reference = "T194", Lot = "2" },
            new Tool { ToolId = marker + "-BQ3", Type = ToolType.Bq, Reference = "T194", Lot = "3" });
        await db.SaveChangesAsync();
        var jobOnId = 0;
        try
        {
            jobOnId = await SeedProductionWithToolsAsync(db, marker, marker + "-CM", marker + "-MF", marker + "-BQ1");
            var machineId = await db.JobOns.Where(j => j.Id == jobOnId).Select(j => j.MachineId).SingleAsync();

            // O que foi registado: o movimento, o bq_id e a BQ do momento.
            var registados = new List<(int MovementId, int BqId, string Bq)>();
            async Task<int> CurrentBqIdAsync()
            {
                await using var leitura = CreateContext();
                return await leitura.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            }

            async Task RegistarAsync(string bq, string tipo, int quantidade)
            {
                var bqId = await CurrentBqIdAsync();
                await using var ctx = CreateContext();
                var outcome = await new RegisterBqMovementHandler(ctx, new NoRouteSettings())
                    .HandleForContextAsync(new RegisterBqMovementForContext(bqId, tipo, quantidade));
                Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
                registados.Add((outcome.Movement!.MovementId, bqId, marker + bq));
            }

            async Task TrocarAsync(string bq)
            {
                await using var ctx = CreateContext();
                var outcome = await new UpdateJobOnHandler(ctx).HandleAsync(new UpdateJobOn(
                    jobOnId, machineId, marker + "-CM", marker + "-MF", marker + bq));
                Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
            }

            await RegistarAsync("-BQ1", "saida", 10);
            await RegistarAsync("-BQ1", "entrada", 4);
            await TrocarAsync("-BQ2");
            await RegistarAsync("-BQ2", "saida", 7);
            await TrocarAsync("-BQ3");
            await RegistarAsync("-BQ3", "saida", 5);

            // Voltar à BQ1 retoma o bq_id dela: o movimento novo junta-se aos
            // dela, no mesmo registo.
            await TrocarAsync("-BQ1");
            await RegistarAsync("-BQ1", "entrada", 6);

            // Três BQ, três bq_id diferentes, cada um com o seu registo.
            Assert.Equal(3, registados.Select(r => r.BqId).Distinct().Count());

            await using (var leitura = CreateContext())
            {
                foreach (var (movementId, bqId, bq) in registados)
                {
                    var lido = await leitura.BqMovements.AsNoTracking()
                        .Where(m => m.Id == movementId)
                        .Select(m => new { m.Trace.BqContextId, ToolId = m.Trace.BqContext!.ToolId })
                        .SingleAsync();
                    Assert.Equal((bqId, bq), (lido.BqContextId!.Value, lido.ToolId));
                }

                // Um só registo por bq_id, sem movimentos de outra BQ.
                foreach (var grupo in registados.GroupBy(r => r.BqId))
                {
                    var traces = await leitura.BqRepairTraces.AsNoTracking()
                        .Where(t => t.BqContextId == grupo.Key).Select(t => t.Id).ToListAsync();
                    Assert.Single(traces);
                    Assert.Equal(
                        grupo.Select(r => r.MovementId).Order(),
                        await leitura.BqMovements.Where(m => m.BqRepairTraceId == traces[0])
                            .Select(m => m.Id).OrderBy(id => id).ToListAsync());
                }
            }
        }
        finally
        {
            if (jobOnId != 0)
            {
                // A limpeza comum não apaga registos nem movimentos da BQ.
                var bqIds = db.BqContexts
                    .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
                    .Select(c => c.Id);
                var traceIds = db.BqRepairTraces
                    .Where(t => t.BqContextId != null && bqIds.Contains(t.BqContextId.Value))
                    .Select(t => t.Id);
                await db.BqMovements.Where(m => traceIds.Contains(m.BqRepairTraceId)).ExecuteDeleteAsync();
                await db.BqRepairTraces
                    .Where(t => t.BqContextId != null && bqIds.Contains(t.BqContextId.Value))
                    .ExecuteDeleteAsync();
            }

            await DeleteProductionAsync(db, marker);
        }
    }
}

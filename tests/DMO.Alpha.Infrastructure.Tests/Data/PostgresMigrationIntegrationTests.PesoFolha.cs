using System.Data.Common;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Budgets preserve the pre-extraction call graph: context + sheet list
    // (+ current combination) + batched facts, with support/association only
    // on their original branches. This is not a latency benchmark.
    [SkippableTheory]
    [InlineData(0, 3)] // Missing control.
    [InlineData(1, 7)] // No Peso yet; offer association.
    [InlineData(2, 8)] // Old Peso only; do not open it implicitly.
    [InlineData(3, 4)] // Current Peso; reuse already loaded technical facts.
    [InlineData(4, 6)] // Explicit old Peso; read support for its own CM.
    public async Task PesoFolha_CarregamentoSoLeSemAumentarConsultas(int scenario, int budget)
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "PF-" + Guid.NewGuid().ToString("N")[..8];
        var oldCm = marker + "-old";
        var currentCm = marker + "-cm";
        var bq = marker + "-bq";
        var mf = marker + "-mf";
        var machine = new Machine { Code = marker };
        var production = new JobOn { Machine = machine, Reference = marker, ProductionNumber = marker, CaloteMm = 9m };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = production };
        db.Controlos.Add(controlo);
        db.Tools.AddRange(
            new Tool { ToolId = oldCm, Type = ToolType.Cm, Reference = marker, Lot = "1" },
            new Tool { ToolId = currentCm, Type = ToolType.Cm, Reference = marker, Lot = "2" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = marker, Lot = "3" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = marker, Lot = "4" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = oldCm, Process = CmProcess.Nnpb, TpDiametroMm = 40m, TpSagittaMm = 6m },
            new ToolTechnicalContext { ToolId = currentCm, Process = CmProcess.Nnpb, TpDiametroMm = 50m, TpSagittaMm = 8m },
            new ToolTechnicalContext { ToolId = bq, VolumeMarisaCm3 = 12m });
        db.CmContexts.Add(new CmContext { JobOn = production, ToolId = currentCm });
        db.BqContexts.Add(new BqContext { JobOn = production, ToolId = bq });
        db.MfContexts.Add(new MfContext { JobOn = production, ToolId = mf });
        await db.SaveChangesAsync();

        Peso AddPeso(int? owner, string cm)
        {
            var peso = new Peso
            {
                ControloId = owner, CmToolId = cm, BqToolId = bq, Process = "NNPB",
                WaterTemperatureC = 20, WaterDensityGramsPerCm3 = 0.9982m,
                GlassDensityGramsPerCm3 = 2.4m, VolumePuncaoCm3 = 7m, VolumeMarisaCm3 = 12m,
                CmToolReferenceFrozen = marker, CmToolLotFrozen = "1"
            };
            db.Pesos.Add(peso);
            return peso;
        }

        var old = scenario >= 2 ? AddPeso(controlo.Id, oldCm) : null;
        var current = scenario >= 3 ? AddPeso(controlo.Id, currentCm) : null;
        var candidate = AddPeso(null, currentCm);
        await db.SaveChangesAsync();
        var before = await db.Pesos.AsNoTracking().Where(p => p.BqToolId == bq)
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.ControloId, p.VolumePuncaoCm3 }).ToListAsync();
        db.ChangeTracker.Clear();

        var loader = new PesoFolhaQuery(new ProductionContextQuery(db), new PesoQuery(db),
            new PesoToolFactsQuery(db), new PesoCapSupportQuery(db), new PesoAssociationQuery(db));
        commands.Reading = true;
        var sheet = await loader.CarregarAsync(scenario == 0 ? -1 : controlo.Id,
            scenario == 4 ? old!.Id : null, null);
        commands.Reading = false;

        Assert.Equal(budget, commands.Reads);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(scenario switch { 3 => current!.Id, 4 => old!.Id, _ => (int?)null }, sheet.Peso?.PesoId);
        if (scenario is 1 or 2)
        {
            Assert.Equal(candidate.Id, Assert.Single(sheet.AssociationCandidates).PesoId);
        }
        else
        {
            Assert.Empty(sheet.AssociationCandidates);
        }
        Assert.Equal(before, await db.Pesos.AsNoTracking().Where(p => p.BqToolId == bq)
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.ControloId, p.VolumePuncaoCm3 }).ToListAsync());
        Assert.Equal(9m, await db.JobOns.AsNoTracking().Where(j => j.Id == production.Id).Select(j => j.CaloteMm).SingleAsync());
        Assert.Equal(8m, await db.ToolTechnicalContexts.AsNoTracking().Where(t => t.ToolId == currentCm).Select(t => t.TpSagittaMm).SingleAsync());
        // Disposing the transaction rolls back this test's fixture only.
    }

    private sealed class PesoFolhaCommands : DbCommandInterceptor
    {
        public bool Reading { get; set; }
        public int Reads { get; private set; }

        private void Observe(DbCommand command)
        {
            if (!Reading) return;
            Assert.StartsWith("SELECT", command.CommandText.TrimStart(), StringComparison.OrdinalIgnoreCase);
            Reads++;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Observe(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Assert.False(Reading, "Sheet loading must not execute a write command.");
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(NonQueryExecuting(command, eventData, result));

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Observe(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ScalarExecuting(command, eventData, result));
    }
}

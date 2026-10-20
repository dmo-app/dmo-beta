using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Planeamento;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Sumário do Planeamento traduzido no PostgreSQL: o InMemory dos testes da
/// página avalia o LINQ em C# e não apanha uma consulta que o Npgsql não
/// traduz. Só corre com DMO_POSTGRES_CONNECTION numa base descartável cujo
/// nome contenha «test» (a do CI local); sem ela, salta.
/// </summary>
public sealed class PlaneamentoSumarioPostgresTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");

    [SkippableFact]
    public async Task TodosOsTipos_NoPostgres()
    {
        Skip.If(string.IsNullOrWhiteSpace(Connection), "DMO_POSTGRES_CONNECTION is not configured (disposable PostgreSQL test database).");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(Connection!) { Pooling = false };
        Skip.IfNot(
            builder.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "DMO_POSTGRES_CONNECTION must name a disposable database containing «test».");

        await using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(builder.ConnectionString).Options);
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var agora = DateTime.UtcNow;
        var machine = new Machine { Code = $"PGS-{p}" };
        db.Machines.Add(machine);
        var porVer = new JobOn { Reference = $"S{p}A", ProductionNumber = "1", Machine = machine, InicioEm = hoje };
        var visto = new JobOn { Reference = $"S{p}B", ProductionNumber = "2", Machine = machine, InicioEm = hoje.AddDays(-5), CriadoEmUtc = agora };
        db.JobOns.AddRange(porVer, visto);
        db.Tools.Add(new Tool { ToolId = $"BQ-PGS-{p}", Type = ToolType.Bq, Reference = $"R{p}", Lot = "4" });
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = visto.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        db.BqContexts.Add(new BqContext { JobOnId = porVer.Id, ToolId = $"BQ-PGS-{p}" });
        db.ControloViewings.Add(new ControloViewing { ControloId = controlo.Id, OperatorId = p[..4], ViewedAtUtc = agora });
        db.ControloDecisoes.Add(new ControloDecisao { ControloId = controlo.Id, Parte = ControloParte.Peso, Aprovado = false, OperatorId = p[..4], EmUtc = agora, Nota = "Refazer" });
        db.JobOnChanges.Add(new JobOnChange { JobOnId = visto.Id, Field = JobOnChangeFields.Machine, OldValueFrozen = "B1", NewValueFrozen = "B2", ChangedAtUtc = agora });
        db.BqMovementDeletions.Add(new BqMovementDeletion
        {
            BqMovementId = int.Parse(p, System.Globalization.CultureInfo.InvariantCulture), BqRepairTraceId = -1,
            Type = BqMovementType.Entrada, Quantity = 2, OccurredAtUtc = agora, MachineCodeFrozen = "C2", DeletedByNameFrozen = $"Apagou {p}", DeletedAtUtc = agora
        });
        await db.SaveChangesAsync();

        try
        {
            var query = new SumarioQuery(db, new ControloRevisaoQuery(db));
            var items = await query.PeriodAsync(hoje.AddDays(-7), hoje.AddDays(1), SumarioTipos.Todos);
            var meus = items.Where(i => i.Titulo.Contains(p, StringComparison.Ordinal)
                || i.Detalhe?.Contains(p, StringComparison.Ordinal) == true
                || i.Por?.Contains(p, StringComparison.Ordinal) == true).ToList();

            Assert.Contains(meus, i => i.Tipo == SumarioTipo.ProducaoComeca && i.JobOnId == porVer.Id);
            Assert.Contains(meus, i => i.Tipo == SumarioTipo.ProducaoComeca && i.JobOnId == visto.Id);
            Assert.Equal(porVer.Id, Assert.Single(meus, i => i.Tipo == SumarioTipo.JobOnPorVer).JobOnId);
            Assert.Equal(visto.Id, Assert.Single(meus, i => i.Tipo == SumarioTipo.JobOnCriado).JobOnId);
            Assert.Equal("Mudou a máquina: B1 → B2", Assert.Single(meus, i => i.Tipo == SumarioTipo.JobOnAlterado).Detalhe);
            Assert.Equal("Refazer", Assert.Single(meus, i => i.Tipo == SumarioTipo.ControloNaoAprovado).Detalhe);
            var apagado = Assert.Single(meus, i => i.Tipo == SumarioTipo.BqMovimentoApagado);
            Assert.Equal("Entrada de 2", apagado.Detalhe);
            Assert.Equal("C2", apagado.Maquina);
            var emFalta = Assert.Single(meus, i => i.Tipo == SumarioTipo.FerramentaDadosEmFalta);
            Assert.Equal($"BQ-PGS-{p}", emFalta.ToolId);
            Assert.StartsWith("Falta: marisa, gargalo · ", emFalta.Detalhe);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.BqMovementDeletions.Where(d => d.DeletedByNameFrozen == $"Apagou {p}").ExecuteDeleteAsync();
            await db.JobOnChanges.Where(c => c.JobOnId == visto.Id).ExecuteDeleteAsync();
            await db.BqContexts.Where(c => c.JobOnId == porVer.Id).ExecuteDeleteAsync();
            await db.ControloDecisoes.Where(d => d.ControloId == controlo.Id).ExecuteDeleteAsync();
            await db.ControloViewings.Where(v => v.ControloId == controlo.Id).ExecuteDeleteAsync();
            await db.Controlos.Where(c => c.Id == controlo.Id).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == $"BQ-PGS-{p}").ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}

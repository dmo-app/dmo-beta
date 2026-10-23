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

public sealed class PlaneamentoSumarioQueryTests
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PorAprovar_MantemOsResultadosDaRevisao_SemCalcularPesos(bool postgres)
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>();
        if (postgres)
        {
            var connection = Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");
            Skip.If(string.IsNullOrWhiteSpace(connection), "Disposable PostgreSQL is not configured.");
            var cs = new Npgsql.NpgsqlConnectionStringBuilder(connection!);
            Skip.IfNot(cs.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true, "Requires a test database.");
            options.UseNpgsql(cs.ConnectionString);
        }
        else
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        await using var db = new DmoDbContext(options.Options);
        if (postgres) await db.Database.MigrateAsync();
        await using var transaction = postgres ? await db.Database.BeginTransactionAsync() : null;
        var prefix = Guid.NewGuid().ToString("N")[..12];
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var agora = DmoBusinessTime.ToUtc(hoje.ToDateTime(new TimeOnly(12, 0)));
        var machine = new Machine { Code = prefix };
        var cm = new Tool { ToolId = $"CM-{prefix}", Type = ToolType.Cm, Reference = prefix, Lot = "1" };
        var bq = new Tool { ToolId = $"BQ-{prefix}", Type = ToolType.Bq, Reference = prefix, Lot = "1" };
        var outraBq = new Tool { ToolId = $"BQ2-{prefix}", Type = ToolType.Bq, Reference = prefix, Lot = "2" };
        db.AddRange(machine, cm, bq, outraBq);
        var controlos = Enumerable.Range(0, 9).Select(i => new DMO.Alpha.Core.Controlo.Controlo
        {
            JobOn = new JobOn { Machine = machine, Reference = prefix, ProductionNumber = i.ToString(), InicioEm = hoje }
        }).ToArray();
        db.Controlos.AddRange(controlos);
        await db.SaveChangesAsync();

        void Peso(int i, DateTime? em, string por, Tool? ferramenta = null) => db.Pesos.Add(new Peso
        {
            ControloId = controlos[i].Id, CmTool = cm, BqTool = ferramenta ?? bq, Process = "NNPB",
            RegistadoEmUtc = em, RegistadoPor = por
        });
        void Pegamentos(int i, DateTime em) => db.Pegamentos.Add(new Pegamentos
        {
            ControloId = controlos[i].Id, RegistadoEmUtc = em, RegistadoPor = $"peg-{i}"
        });
        // Peso takes precedence over an earlier Pegamentos; earliest Peso wins.
        Peso(0, agora, "peso-primeiro");
        Peso(0, agora.AddHours(1), "peso-segundo", outraBq);
        Pegamentos(0, agora.AddDays(-1));
        Pegamentos(1, agora);
        Peso(2, null, "sem-data");
        Pegamentos(2, agora);
        foreach (var i in new[] { 3, 4, 5 })
        {
            Peso(i, agora, $"peso-{i}");
            db.ControloDecisoes.Add(new ControloDecisao
            {
                ControloId = controlos[i].Id, Parte = ControloParte.Peso,
                Aprovado = i != 4, OperatorId = "test", EmUtc = agora.AddMinutes(1)
            });
        }
        Peso(5, agora.AddMinutes(2), "novo-peso", outraBq);
        // Empty control, outside period, and legacy approval stay out.
        Pegamentos(7, agora.AddDays(-3));
        Peso(8, agora, "legado");
        controlos[8].ApprovedAtUtc = agora;
        await db.SaveChangesAsync();

        var revisao = await new ControloRevisaoQuery(db).PeriodAsync(
            hoje, hoje.AddDays(1), null, ControloRevisaoEstado.Pendente, null, 500);
        var query = new SumarioQuery(db);
        var items = await query.PeriodAsync(hoje, hoje.AddDays(1), [SumarioTipo.ControloPorAprovar]);
        var meus = items.Where(i => i.Titulo.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 5 }.Select(i => controlos[i].JobOnId).Order(), meus.Select(i => i.JobOnId!.Value).Order());
        foreach (var item in meus)
        {
            var original = Assert.Single(revisao, r => r.JobOnId == item.JobOnId);
            Assert.Equal(original.RegistadoEmUtc, item.QuandoUtc);
            Assert.Equal(original.RegistadoPor, item.Por);
            Assert.Equal(original.Machine, item.Maquina);
            Assert.Equal($"{original.Reference} {original.ProductionNumber}", item.Titulo);
        }
        var importantes = await query.PorResolverAsync(hoje, [SumarioTipo.ControloPorAprovar]);
        Assert.All(meus, item => Assert.Contains(importantes, i => i.JobOnId == item.JobOnId && i.QuandoUtc == item.QuandoUtc));
    }
}

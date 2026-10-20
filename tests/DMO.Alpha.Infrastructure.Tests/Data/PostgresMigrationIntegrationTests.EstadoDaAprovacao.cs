using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O estado da aprovação no PostgreSQL (sweep de 19/10/2026, C-1 e DB-4; o
/// especialista, R28): calcula-se ao ler, das decisões das partes e dos
/// registos, e o calendário, a lista, o Histórico e o aviso do Job On
/// traduzem essa regra no SQL. Duas decisões ao mesmo tempo em partes
/// diferentes resumem-se uma de cada vez, e o resumo gravado sai das duas.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task EstadoDaAprovacao_SoOPesoDecide_PegamentosDepoisNaoMudam_PesoNovoVoltaAosPendentes_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // Um mês só destes dados (um ano ao acaso no passado), para o
        // calendário não contar os controlos de outros testes.
        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var ano = Random.Shared.Next(1950, 2000);
        var registadoEm = new DateTime(ano, 2, 12, 10, 0, 0, DateTimeKind.Utc);
        var (machine, jobOn, controloId) = await NovaProducaoComPesoAsync(db, p, registadoEm);
        try
        {
            var handler = new ControloApprovalHandler(db);
            var revisao = new ControloRevisaoQuery(db);
            var historico = new ControloHistorySearchQuery(db);
            var avisos = new JobOnChangeQuery(db);
            async Task<ControloRevisaoItem?> NoDiaAsync(ControloRevisaoEstado estado) =>
                (await revisao.DayAsync(new DateOnly(ano, 2, 12), jobOn.Reference, estado)).SingleOrDefault();

            var aprovado = await handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
            Assert.Equal("4802", aprovado!.ApprovedByOperatorId);
            Assert.Equal(ControloRevisaoEstado.Aprovado, (await NoDiaAsync(ControloRevisaoEstado.Aprovado))!.Estado);
            Assert.Equal(new ControloRevisaoDia(12, 1, 0), Assert.Single(await revisao.MonthAsync(ano, 2)));
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOn.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "9.5", NewValueFrozen = "10",
                ChangedAtUtc = aprovado.ApprovedAtUtc!.Value.AddSeconds(1)
            });
            await db.SaveChangesAsync();
            Assert.NotNull(await avisos.ForControloApprovalAsync(controloId));

            // Os Pegamentos medidos depois não se aprovam (Diogo, 19/10/2026):
            // o controlo continua aprovado.
            db.Pegamentos.Add(new Pegamentos
            {
                ControloId = controloId, CmToolId = $"CM-EA-{p}", ToleranceMm = 0.20m, RegistadoEmUtc = registadoEm.AddHours(1)
            });
            await db.SaveChangesAsync();
            Assert.Equal("4802", (await handler.GetAsync(controloId))!.ApprovedByOperatorId);
            Assert.Equal(new ControloRevisaoDia(12, 1, 0), Assert.Single(await revisao.MonthAsync(ano, 2)));

            // Um Peso novo logo depois (a CM trocou): volta aos pendentes do
            // calendário e do filtro, e o aviso sai.
            db.Tools.Add(new Tool { ToolId = $"CM2-EA-{p}", Type = ToolType.Cm, Reference = p, Lot = "3" });
            db.Pesos.Add(new Peso
            {
                CmToolId = $"CM2-EA-{p}", BqToolId = $"BQ-EA-{p}", Process = "NNPB", WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
                ControloId = controloId, RegistadoEmUtc = aprovado.ApprovedAtUtc!.Value.AddTicks(10)
            });
            await db.SaveChangesAsync();
            Assert.Equal(new ControloApprovalReadModel(null, null), await handler.GetAsync(controloId));
            Assert.Null(await NoDiaAsync(ControloRevisaoEstado.Aprovado));
            var pendente = await NoDiaAsync(ControloRevisaoEstado.Pendente);
            Assert.Null(pendente!.DecididoPor);
            Assert.Equal(new ControloRevisaoDia(12, 1, 1), Assert.Single(await revisao.MonthAsync(ano, 2)));
            var resumo = Assert.Single((await historico.SearchPageAsync(
                null, jobOn.ProductionNumber, null, ControloHistoryKind.Resumo, null, 1, 50)).Items);
            Assert.Equal("Por aprovar", resumo.Resumo);
            Assert.Null(await avisos.ForControloApprovalAsync(controloId));

            // Não aprovar o Peso: não aprovado, com a nota.
            await handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: false, "4801", "Repetir o Peso.");
            var naoAprovado = await NoDiaAsync(ControloRevisaoEstado.NaoAprovado);
            Assert.Equal("4801", naoAprovado!.DecididoPor);
            var gravado = await db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Null(gravado.ApprovedAtUtc);
            Assert.Equal("4801", gravado.NotApprovedByOperatorId);
            Assert.Equal("Repetir o Peso.", gravado.ReviewNote);

            // Aprovado outra vez: com o dia da aprovação no Histórico.
            var depois = await handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4803", null);
            Assert.Equal("4803", depois!.ApprovedByOperatorId);
            Assert.Equal("4803", (await NoDiaAsync(ControloRevisaoEstado.Aprovado))!.DecididoPor);
            var dia = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(depois.ApprovedAtUtc!.Value);
            Assert.Equal("Aprovado por 4803", Assert.Single((await historico.SearchPageAsync(
                null, jobOn.ProductionNumber, null, ControloHistoryKind.Resumo, DateOnly.FromDateTime(dia), 1, 50)).Items).Resumo);
        }
        finally
        {
            await ApagarProducaoAsync(db, p, machine, controloId);
        }
    }

    [SkippableFact]
    public async Task EstadoDaAprovacao_DuasDecisoesAoMesmoTempo_OResumoEODaUltima_NoPostgres()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var registadoEm = new DateTime(2001, 5, 3, 10, 0, 0, DateTimeKind.Utc);
        var (machine, _, controloId) = await NovaProducaoComPesoAsync(setup, p, registadoEm);
        setup.Pegamentos.Add(new Pegamentos
        {
            ControloId = controloId, CmToolId = $"CM-EA-{p}", ToleranceMm = 0.20m, RegistadoEmUtc = registadoEm
        });
        await setup.SaveChangesAsync();
        try
        {
            // Um «Não aprovar» do Peso de outro chefe chega quando a
            // aprovação já calculou o resumo e o vai gravar: o resumo gravado
            // é o da última decisão.
            var durante = new DuranteOResumo(async () =>
            {
                await using var other = CreateContext();
                await new ControloApprovalHandler(other).DecidePartAsync(
                    controloId, ControloParte.Peso, aprovado: false, "2002", "Repetir os pegamentos.");
            });
            await using (var db = CreateContextWith(durante))
            {
                await new ControloApprovalHandler(db).DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "1001", null);
            }

            await durante.Outro!;
            var gravado = await setup.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Null(gravado.ApprovedByOperatorId);
            Assert.Null(gravado.ApprovedAtUtc);
            Assert.Equal("2002", gravado.NotApprovedByOperatorId);
            Assert.Equal("Repetir os pegamentos.", gravado.ReviewNote);
            var lido = await new ControloApprovalHandler(setup).GetAsync(controloId);
            Assert.Equal(new ControloApprovalReadModel(
                null, null, gravado.NotApprovedByOperatorId, gravado.NotApprovedAtUtc, gravado.ReviewNote), lido);
        }
        finally
        {
            await ApagarProducaoAsync(setup, p, machine, controloId);
        }
    }

    /// <summary>
    /// Corre a decisão «concorrente» na segunda gravação do pedido em teste
    /// (a do resumo, já calculado) e espera por ela no máximo um segundo:
    /// com o trinco, a outra fica à espera até este pedido acabar.
    /// </summary>
    private sealed class DuranteOResumo(Func<Task> concurrent) : SaveChangesInterceptor
    {
        private int _gravacoes;

        public Task? Outro { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (++_gravacoes == 2)
            {
                Outro = Task.Run(concurrent, CancellationToken.None);
                await Task.WhenAny(Outro, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));
            }

            return result;
        }
    }

    private static async Task<(Machine Machine, JobOn JobOn, int ControloId)> NovaProducaoComPesoAsync(
        DmoDbContext db, string p, DateTime registadoEm)
    {
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var machine = new Machine { Code = $"EA-{letras}" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = $"CM-EA-{p}", Type = ToolType.Cm, Reference = p, Lot = "1" },
            new Tool { ToolId = $"BQ-EA-{p}", Type = ToolType.Bq, Reference = $"E{p}", Lot = "2" });
        await db.SaveChangesAsync();
        var jobOn = new JobOn { Reference = $"{p}E{p}", ProductionNumber = $"EA{p}", MachineId = machine.Id };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        db.Pesos.Add(new Peso
        {
            CmToolId = $"CM-EA-{p}", BqToolId = $"BQ-EA-{p}", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id, RegistadoEmUtc = registadoEm
        });
        await db.SaveChangesAsync();
        return (machine, jobOn, controlo.Id);
    }

    private static async Task ApagarProducaoAsync(DmoDbContext db, string p, Machine machine, int controloId)
    {
        db.ChangeTracker.Clear();
        await db.JobOnChanges.Where(c => c.JobOn!.MachineId == machine.Id).ExecuteDeleteAsync();
        await db.Pegamentos.Where(g => g.ControloId == controloId).ExecuteDeleteAsync();
        await db.Pesos.Where(x => x.ControloId == controloId).ExecuteDeleteAsync();
        await DeleteJobOnAsync(db, machine.Id);
        await db.Tools.Where(t => t.ToolId.EndsWith(p)).ExecuteDeleteAsync();
    }
}

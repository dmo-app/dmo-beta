using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A história de uma produção (Diogo, 22/10/2026, A-0046): o Job On 202601 na
/// B1 com a CM 5447 L3, a BQ T173 L01 e o MF 5612 L2, e o que cada módulo
/// regista nela (Boquilhas, Peso aprovado, Pegamentos, Reparação Interna,
/// Armazém). Tudo pelos serviços reais; só a semente mínima (ferramentas,
/// utilizador e linhas) entra direta, como nos outros testes desta classe.
/// Depois, só leituras: cada módulo vê o mesmo. A referência total do Job On
/// segue a regra da app (ToolCandidateQuery.SplitReference): o modelo do MF
/// seguido da parte BQ, 5612T173 (e 9162T173 no outro Job On da mesma BQ).
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static readonly DateOnly HistoriaInicio = new(2026, 9, 1);
    private static readonly DateOnly HistoriaFim = new(2026, 9, 10);
    private static readonly DateOnly HistoriaFimSegundo = new(2026, 9, 20);
    private static readonly DateOnly HistoriaInicioOutraBq = new(2026, 9, 21);
    private static readonly DateOnly HistoriaFimOutraBq = new(2026, 9, 25);
    private const string HistoriaPosicao = "4152";
    private const string HistoriaReparador = "Moldin";

    private sealed class HistoriaCenario
    {
        public required string Marca { get; init; }
        public required string Cm { get; init; }
        public required string Mf { get; init; }
        public required string Bq { get; init; }
        public required string Bq02 { get; init; }
        public required string Mf9162 { get; init; }
        public required Guid Quem { get; init; }
        public List<int> MaquinasCriadas { get; } = [];
        public int B1 { get; set; }
        public int B2 { get; set; }
        public bool PosicaoCriada { get; set; }
        public int? ReparadorCriado { get; set; }
        public int ReparadorId { get; set; }
        public List<Guid> Operacoes { get; } = [];
        public List<int> Traces { get; } = [];
        public int JobOn1 { get; set; }
        public int Controlo1 { get; set; }
        public int Cm1 { get; set; }
        public int Mf1 { get; set; }
        public int Bq1 { get; set; }
        public int JobOn2 { get; set; }
        public int JobOn3 { get; set; }
        public int Bq3 { get; set; }
        public int PesoId { get; set; }
        public int RiCm { get; set; }
        public int RiMf { get; set; }
    }

    [SkippableFact]
    public async Task HistoriaDeUmaProducao_TodosOsModulosVeemOMesmo()
    {
        SkipWithoutConnection();
        var c = await HistoriaSementeAsync();
        try
        {
            await HistoriaRegistosAsync(c);

            await using var db = CreateContext();

            // A ficha da CM: o Job On, o estado e a posição do Armazém.
            var producoes = await new FerramentasFabricoSap(db, new SemSessao()).ProducoesAsync(c.Cm, null, 50);
            var naFicha = Assert.Single(producoes.Pagina.Items, p => p.JobOnId == c.JobOn1);
            Assert.Equal(("5612T173", "202601", "B1", (DateOnly?)HistoriaInicio, (DateOnly?)HistoriaFim),
                (naFicha.Referencia, naFicha.Producao, naFicha.Maquina, naFicha.InicioEm, naFicha.FimEm));
            var situacao = await Movimentos(db).SituacaoAsync(c.Cm);
            Assert.NotNull(situacao);
            Assert.Equal((HistoriaPosicao, (FerramentaEstadoTipo?)FerramentaEstadoTipo.Reparado, FerramentaLocalizacaoTipo.Fabrico),
                (situacao!.Posicao, situacao.Estado, situacao.Localizacao));
            // De volta ao armazém, a última Saída só fica nos últimos movimentos.
            Assert.Null(situacao.UltimaSaida);
            Assert.Contains(situacao.UltimosMovimentos, m => m.Tipo == ArmazemMovimentoTipo.Saida);

            // A RI e o Peso da CM (a ficha da CM não os mostra: lidos pela
            // Consulta da RI e pelo Peso do controlo, as APIs reais).
            var ri = await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(JobOnId: c.JobOn1), null);
            var riCm = Assert.Single(ri.Items, l => l.Tipo == ReparacaoTipo.Cm);
            Assert.Equal((c.RiCm, (int?)c.JobOn1, "202601", "5612T173", "B1", c.Cm, new DateOnly(2026, 9, 3)),
                (riCm.Id, riCm.JobOnId, riCm.Producao, riCm.Referencia, riCm.Maquina, riCm.FerramentaToolId, riCm.Dia));
            Assert.Equal(c.Cm1, riCm.Ferramenta!.ContextoId);
            var pesos = await new PesoQuery(db).ListByControloAsync(c.Controlo1);
            var peso = Assert.Single(pesos);
            Assert.Equal((c.PesoId, c.Cm, c.Bq), (peso.PesoId, peso.CmToolId, peso.BqToolId));

            // O Resumo do Controlo: a produção, o Peso (aprovado) e os Pegamentos.
            var resumo = await new ResumoProductionQuery(db).ListAsync("5612T173", 1);
            Assert.Contains(resumo.Items, i => i.JobOnId == c.JobOn1 && i.ProductionNumber == "202601" && i.Machine == "B1");
            Assert.Equal(c.Controlo1, await new EnsureControloForJobOnHandler(db).FindExistingIdAsync(c.JobOn1));
            Assert.Equal("4802", (await new ControloApprovalHandler(db).GetAsync(c.Controlo1))!.ApprovedByOperatorId);
            var folhas = await PegamentosLeitura(db).ListSheetsByControloAsync(c.Controlo1);
            Assert.Single(folhas);

            // O Histórico da BQ: 5612T173 com 2 movimentos, do bq_id do Job On.
            var historico = new BqToolHistoryQuery(db);
            var movimentos = await historico.ListMovementsAsync(null, null, null, null, null, null, toolId: c.Bq);
            var doJobOn1 = movimentos.Where(m => m.Reference == "5612T173").ToList();
            Assert.Equal(2, doJobOn1.Count);
            Assert.All(doJobOn1, m => Assert.Equal(("202601", "B1"), (m.ProductionNumber, m.MachineCode)));
            Assert.Equal(new[] { "Entrada", "Saida" }, doJobOn1.Select(m => m.MovementType).OrderBy(t => t));
            Assert.Equal(c.Bq1, await BqIdDoTraceAsync(db, doJobOn1[0].BqRepairTraceId));

            // A RI ligada ao Job On certo (e ao cm_id dele).
            var gravada = await db.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == c.RiCm);
            Assert.Equal(((int?)c.JobOn1, (int?)c.Cm1), (gravada.JobOnId, gravada.CmContextId));

            // Os Movimentos do Armazém da CM: a ida e a volta da Reparação.
            var armazem = await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(ToolId: c.Cm, Reparador: HistoriaReparador), 1);
            Assert.Equal(2, armazem.TotalCount);
            Assert.Contains(armazem.Items, l => l.Movimento == ArmazemMovimentoTipo.Saida && l.Destino == ArmazemDestino.Reparacao);
            Assert.Contains(armazem.Items, l => l.Movimento == ArmazemMovimentoTipo.Entrada && l.Posicao == HistoriaPosicao);

            // O segundo Job On da mesma CM, a começar no dia em que o primeiro
            // acaba: sem sobreposição e nada do primeiro passa para ele.
            var segundo = await new ProductionContextQuery(db).GetAsync(c.JobOn2);
            Assert.NotNull(segundo);
            Assert.NotEqual(c.Cm1, segundo!.CmId);
            Assert.Equal(c.Cm, segundo.CmToolId);
            Assert.Empty((await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(JobOnId: c.JobOn2), null)).Items);
            var controlo2 = await new EnsureControloForJobOnHandler(db).FindExistingIdAsync(c.JobOn2);
            Assert.NotNull(controlo2);
            Assert.Empty(await new PesoQuery(db).ListByControloAsync(controlo2!.Value));
            Assert.Empty(await PegamentosLeitura(db).ListSheetsByControloAsync(controlo2.Value));
            Assert.DoesNotContain(movimentos, m => m.ProductionNumber == "202602");
            Assert.Equal(2, producoes.Pagina.Items.Count(p => p.JobOnId == c.JobOn1 || p.JobOnId == c.JobOn2));

            // A mesma BQ T173 lote 01 noutro Job On, com o MF 9162: o
            // histórico mostra os dois, cada um com o seu bq_id, sem misturar.
            var jobs = (await historico.ListJobHistoryAsync(c.Bq, null)).Items;
            Assert.Contains(jobs, j => j.JobOnId == c.JobOn1 && j.Reference == "5612T173");
            Assert.Contains(jobs, j => j.JobOnId == c.JobOn3 && j.Reference == "9162T173");
            var doJobOn3 = movimentos.Where(m => m.Reference == "9162T173").ToList();
            Assert.Equal(2, doJobOn3.Count);
            Assert.All(doJobOn3, m => Assert.Equal(("202601", "B2", 5), (m.ProductionNumber, m.MachineCode, m.Quantity)));
            Assert.All(doJobOn1, m => Assert.Equal(3, m.Quantity));
            Assert.Equal(c.Bq3, await BqIdDoTraceAsync(db, doJobOn3[0].BqRepairTraceId));
            Assert.NotEqual(c.Bq1, c.Bq3);
            Assert.Empty(doJobOn1.Select(m => m.MovementId).Intersect(doJobOn3.Select(m => m.MovementId)));
            Assert.Equal(4, movimentos.Count);
        }
        finally
        {
            await HistoriaLimparAsync(c);
        }
    }

    [SkippableFact]
    public async Task MudarOJobOn_NaoPerdeNadaDosOutrosModulos()
    {
        SkipWithoutConnection();
        var c = await HistoriaSementeAsync();
        try
        {
            await HistoriaRegistosAsync(c);
            var antes = await HistoriaContagensAsync(c);

            // Mudar as datas (o fim um dia antes; o segundo Job On encosta) e
            // trocar a BQ para o lote 02.
            await using (var db = CreateContext())
            {
                var mudou = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
                    c.JobOn1, c.B1, c.Cm, c.Mf, c.Bq02, new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 9), ChangedBy: "Diogo"));
                Assert.Equal(UpdateJobOnResult.Success, mudou.Result);
                Assert.Null(mudou.OverlapWarning);
                Assert.Equal((c.Cm1, c.Mf1), (mudou.JobOn!.CmId, mudou.JobOn.MfId));
                Assert.NotEqual(c.Bq1, mudou.JobOn.BqId);
            }

            // Eliminar o vizinho (o segundo Job On da mesma CM).
            await using (var db = CreateContext())
            {
                Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(db).HandleAsync(new DeleteJobOn(c.JobOn2, "Diogo")));
            }

            var depois = await HistoriaContagensAsync(c);
            Assert.Equal(antes with { ProducoesDaCm = antes.ProducoesDaCm - 1 }, depois);

            await using var leitura = CreateContext();
            // A RI e o Peso continuam na produção, que continua válida.
            var ri = await leitura.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == c.RiCm);
            Assert.Equal(((int?)c.JobOn1, (int?)c.Cm1), (ri.JobOnId, ri.CmContextId));
            var producao = await new ProductionContextQuery(leitura).GetAsync(c.JobOn1);
            Assert.NotNull(producao);
            Assert.Equal((c.Cm1, c.Mf1, c.Bq02), (producao!.CmId!.Value, producao.MfId!.Value, producao.BqToolId));
            var pesos = await new PesoQuery(leitura).ListByControloAsync(c.Controlo1);
            Assert.Equal(c.PesoId, Assert.Single(pesos).PesoId);
            Assert.Equal(c.Controlo1, await new EnsureControloForJobOnHandler(leitura).FindExistingIdAsync(c.JobOn1));

            // O lote antigo da BQ continua no Histórico, com os 2 movimentos no Job On.
            var historico = new BqToolHistoryQuery(leitura);
            Assert.Contains((await historico.ListJobHistoryAsync(c.Bq, null)).Items, j => j.JobOnId == c.JobOn1);
            var movimentos = await historico.ListMovementsAsync(null, null, null, null, null, null, toolId: c.Bq);
            Assert.Equal(2, movimentos.Count(m => m.Reference == "5612T173" && m.ProductionNumber == "202601"));
            Assert.Contains((await historico.ListJobHistoryAsync(c.Bq02, null)).Items, j => j.JobOnId == c.JobOn1);
            Assert.Empty(await leitura.JobOns.AsNoTracking().Where(j => j.Id == c.JobOn2).ToListAsync());
        }
        finally
        {
            await HistoriaLimparAsync(c);
        }
    }

    [SkippableFact]
    public async Task JobOnAntigo_MostraQuemReparouCadaFerramenta()
    {
        SkipWithoutConnection();
        var c = await HistoriaSementeAsync();
        try
        {
            await HistoriaRegistosAsync(c);

            // O Job On é antigo e está fechado (o fim já passou).
            await using var db = CreateContext();
            var producao = await new ProductionContextQuery(db).GetAsync(c.JobOn1);
            Assert.NotNull(producao);
            Assert.True(producao!.FimEm < DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now));

            // A partir do Job On: quem reparou na máquina (as RI, com o
            // utilizador e o dia) cada ferramenta da produção.
            var ri = (await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(JobOnId: c.JobOn1), null)).Items;
            var riCm = Assert.Single(ri, l => l.Tipo == ReparacaoTipo.Cm);
            var riMf = Assert.Single(ri, l => l.Tipo == ReparacaoTipo.Mf);
            Assert.Equal((producao.CmToolId, "Ana " + c.Marca, new DateOnly(2026, 9, 3)), (riCm.FerramentaToolId, riCm.Quem, riCm.Dia));
            Assert.Equal((producao.MfToolId, "Ana " + c.Marca, new DateOnly(2026, 9, 4)), (riMf.FerramentaToolId, riMf.Quem, riMf.Dia));

            // As reparações do Armazém de cada ferramenta da produção, nos dias
            // do Job On (o movimento não guarda a produção: liga-se pela
            // ferramenta e pelas datas), com o reparador e o dia.
            var armazem = new ArmazemHistorico(db);
            var cm = await armazem.ListarAsync(new ArmazemHistoricoFiltro(
                ToolId: producao.CmToolId, Tipo: ArmazemMovimentoTipo.Saida, Destino: ArmazemDestino.Reparacao,
                De: producao.InicioEm, Ate: producao.FimEm), 1);
            var saida = Assert.Single(cm.Items);
            Assert.Equal((HistoriaReparador, new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc)), (saida.Reparador, saida.OcorridoEmUtc));
            var mf = await armazem.ListarAsync(new ArmazemHistoricoFiltro(
                ToolId: producao.MfToolId, Tipo: ArmazemMovimentoTipo.Saida, Destino: ArmazemDestino.Reparacao,
                De: producao.InicioEm, Ate: producao.FimEm), 1);
            var saidaMf = Assert.Single(mf.Items);
            Assert.Equal((HistoriaReparador, new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc)), (saidaMf.Reparador, saidaMf.OcorridoEmUtc));
        }
        finally
        {
            await HistoriaLimparAsync(c);
        }
    }

    private sealed record HistoriaContagens(
        int Ri, int Pesos, int Pegamentos, int MovimentosBq, int MovimentosArmazem, int ProducoesDaCm, int JobsDaBq);

    private async Task<HistoriaContagens> HistoriaContagensAsync(HistoriaCenario c)
    {
        await using var db = CreateContext();
        var ri = (await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(JobOnId: c.JobOn1), null)).TotalCount;
        var pesos = (await new PesoQuery(db).ListByControloAsync(c.Controlo1)).Count;
        var pegamentos = (await PegamentosLeitura(db).ListSheetsByControloAsync(c.Controlo1)).Count;
        var bq = (await new BqToolHistoryQuery(db).ListMovementsAsync(null, null, null, null, null, null, toolId: c.Bq)).Count;
        var armazem = (await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(ToolId: c.Cm), 1)).TotalCount
            + (await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(ToolId: c.Mf), 1)).TotalCount;
        var producoes = (await new FerramentasFabricoSap(db, new SemSessao()).ProducoesAsync(c.Cm, null, 50)).Pagina.TotalCount;
        var jobs = (await new BqToolHistoryQuery(db).ListJobHistoryAsync(c.Bq, null)).TotalCount;
        return new HistoriaContagens(ri, pesos, pegamentos, bq, armazem, producoes, jobs);
    }

    private static PegamentosQuery PegamentosLeitura(DmoDbContext db) =>
        new(db, new ProductionContextQuery(db), new PegamentosToleranceConfiguration(db));

    private static Task<int?> BqIdDoTraceAsync(DmoDbContext db, int traceId) =>
        db.BqRepairTraces.AsNoTracking().Where(t => t.Id == traceId).Select(t => t.BqContextId).SingleAsync();

    // A semente mínima (como nos outros testes): as ferramentas, o utilizador
    // e as linhas B1/B2 (usadas se já existem). Depois, os Job On pelo serviço.
    private async Task<HistoriaCenario> HistoriaSementeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marca = "HP" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var c = new HistoriaCenario
        {
            Marca = marca, Cm = marca + "-cm5447", Mf = marca + "-mf5612", Bq = marca + "-bqT173",
            Bq02 = marca + "-bqT173-02", Mf9162 = marca + "-mf9162", Quem = Guid.NewGuid()
        };

        // Restos de uma corrida interrompida deste teste (as mesmas produções).
        await HistoriaRestosAsync(db);

        db.Tools.AddRange(
            new Tool { ToolId = c.Cm, Type = ToolType.Cm, Reference = "5447", Lot = "3", AssociatedMfReference = "5612" },
            new Tool { ToolId = c.Mf, Type = ToolType.Mf, Reference = "5612", Lot = "2" },
            new Tool { ToolId = c.Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" },
            new Tool { ToolId = c.Bq02, Type = ToolType.Bq, Reference = "T173", Lot = "02" },
            new Tool { ToolId = c.Mf9162, Type = ToolType.Mf, Reference = "9162", Lot = "1" });
        db.Users.Add(new User
        {
            Id = c.Quem, Name = "Ana " + marca, ProviderUserId = "p-" + marca,
            OperatorId = Random.Shared.Next(1000, 10000).ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        await db.SaveChangesAsync();
        c.B1 = await LinhaAsync(db, "B1", c);
        c.B2 = await LinhaAsync(db, "B2", c);

        var criar = new CreateJobOnHandler(db);
        var primeiro = await criar.HandleAsync(new CreateJobOn("202601", "5612T173", c.B1, c.Cm, c.Mf, c.Bq, HistoriaInicio, HistoriaFim));
        Assert.Equal(CreateJobOnResult.Success, primeiro.Result);
        Assert.Null(primeiro.OverlapWarning);
        c.JobOn1 = primeiro.JobOn!.JobOnId;
        c.Cm1 = primeiro.JobOn.CmId!.Value;
        c.Mf1 = primeiro.JobOn.MfId!.Value;
        c.Bq1 = primeiro.JobOn.BqId!.Value;
        c.Controlo1 = (await new EnsureControloForJobOnHandler(db).FindExistingIdAsync(c.JobOn1))!.Value;

        // O segundo Job On da mesma CM começa no dia em que o primeiro acaba.
        var segundo = await criar.HandleAsync(new CreateJobOn("202602", "5612T173", c.B1, c.Cm, c.Mf, null, HistoriaFim, HistoriaFimSegundo));
        Assert.Equal(CreateJobOnResult.Success, segundo.Result);
        Assert.Null(segundo.OverlapWarning);
        c.JobOn2 = segundo.JobOn!.JobOnId;

        // A mesma BQ T173 lote 01 noutro Job On, com o MF 9162, na B2.
        var outraBq = await criar.HandleAsync(new CreateJobOn("202601", "9162T173", c.B2, null, c.Mf9162, c.Bq, HistoriaInicioOutraBq, HistoriaFimOutraBq));
        Assert.Equal(CreateJobOnResult.Success, outraBq.Result);
        c.JobOn3 = outraBq.JobOn!.JobOnId;
        c.Bq3 = outraBq.JobOn.BqId!.Value;
        return c;
    }

    private async Task<int> LinhaAsync(DmoDbContext db, string codigo, HistoriaCenario c)
    {
        if (await db.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Code == codigo) is { } existente)
        {
            return existente.Id;
        }

        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        await db.SaveChangesAsync();
        c.MaquinasCriadas.Add(maquina.Id);
        return maquina.Id;
    }

    // O que cada módulo regista na produção, pelos serviços reais.
    private async Task HistoriaRegistosAsync(HistoriaCenario c)
    {
        await using var db = CreateContext();

        // Boquilhas: uma saída e uma entrada no bq_id do Job On (e duas no da B2).
        var bq = new RegisterBqMovementHandler(db, new NoRouteSettings());
        foreach (var (contexto, tipo, quantidade, quando, maquina) in new[]
        {
            (c.Bq1, RegisterBqMovementHandler.Saida, 3, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), c.B1),
            (c.Bq1, RegisterBqMovementHandler.Entrada, 3, new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc), c.B1),
            (c.Bq3, RegisterBqMovementHandler.Saida, 5, new DateTime(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc), c.B2),
            (c.Bq3, RegisterBqMovementHandler.Entrada, 5, new DateTime(2026, 9, 23, 9, 0, 0, DateTimeKind.Utc), c.B2)
        })
        {
            var registo = await bq.HandleForContextAsync(new RegisterBqMovementForContext(contexto, tipo, quantidade, "4801", quando, maquina));
            Assert.Equal(RegisterBqMovementResult.Success, registo.Result);
            db.ChangeTracker.Clear();
        }

        c.Traces.AddRange(await db.BqRepairTraces.AsNoTracking()
            .Where(t => t.BqContextId == c.Bq1 || t.BqContextId == c.Bq3).Select(t => t.Id).ToListAsync());

        // O Peso aprovado (a densidade do vidro só se grava se a base não a tem).
        if (!await db.GlassDensities.AnyAsync(g => g.Process == CmProcess.Nnpb))
        {
            Assert.True((await new SaveGlassDensityHandler(db).SaveAsync(2.50m, 2.50m)).Saved);
        }

        // Os valores técnicos da CM pela ficha (o Pegamento precisa do gargalo).
        Assert.Equal(EditToolTechnicalValuesResult.Success, (await new EditToolTechnicalValuesHandler(db).HandleAsync(
            new EditToolTechnicalValues(c.Cm, "NNPB", VolumePuncaoCm3: 30m, DiametroGargaloMm: 70m))).Result);
        db.ChangeTracker.Clear();

        var criado = await new CreatePesoForControloHandler(db, new CreateToolScopedPesoHandler(db, new GlassDensityConfiguration(db)))
            .HandleAsync(new CreatePesoForControlo(c.Controlo1, 20m, "NNPB", 30m, null, 10m));
        Assert.Equal(CreatePesoForControloResult.Success, criado.Result);
        c.PesoId = criado.Peso!.PesoId;
        Assert.Equal(RecordPesoMeasurementResult.Success,
            (await new RecordPesoMeasurementHandler(db).HandleAsync(new RecordPesoMeasurement(c.PesoId, "1", 200m))).Result);
        Assert.NotNull(await new ControloApprovalHandler(db).DecidePartAsync(c.Controlo1, ControloParte.Peso, aprovado: true, "4802", null));

        // Um Pegamento.
        Assert.Equal(RecordPegamentosMeasurementResult.Success,
            (await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db))
                .HandleAsync(new RecordPegamentosMeasurement(c.Controlo1, "CM", 70.30m))).Result);

        // As RI da CM e do MF, dentro das datas do Job On.
        var ri = ReparacoesDe(db, c.Quem);
        var riCm = new PedidoDeReparacao(Guid.NewGuid(), c.B1, c.JobOn1, ReparacaoTipo.Cm, c.Cm1, "17", Dia: new DateOnly(2026, 9, 3));
        var riMf = new PedidoDeReparacao(Guid.NewGuid(), c.B1, c.JobOn1, ReparacaoTipo.Mf, c.Mf1, "8", Dia: new DateOnly(2026, 9, 4));
        c.Operacoes.AddRange([riCm.OperacaoId, riMf.OperacaoId]);
        var gravadaCm = await ri.RegistarAsync(riCm);
        Assert.Equal(ReparacaoResultado.Gravado, gravadaCm.Resultado);
        c.RiCm = gravadaCm.ReparacaoId!.Value;
        var gravadaMf = await ri.RegistarAsync(riMf);
        Assert.Equal(ReparacaoResultado.Gravado, gravadaMf.Resultado);
        c.RiMf = gravadaMf.ReparacaoId!.Value;

        // Armazém: o reparador Moldin (pelo serviço, se ainda não existe) e a
        // posição 4152 (criada na Entrada, se ainda não existe).
        if (await db.Reparadores.AsNoTracking().FirstOrDefaultAsync(r => r.Nome == HistoriaReparador) is { } moldin)
        {
            c.ReparadorId = moldin.Id;
        }
        else
        {
            Assert.Equal(ReparadorResultado.Gravado, await new Reparadores(db).CriarAsync(HistoriaReparador, true, true));
            c.ReparadorId = (await db.Reparadores.AsNoTracking().SingleAsync(r => r.Nome == HistoriaReparador)).Id;
            c.ReparadorCriado = c.ReparadorId;
        }

        c.PosicaoCriada = !await db.ArmazemPosicoes.AnyAsync(p => p.Codigo == HistoriaPosicao);

        // A CM: «Por reparar» até à Saída para a Reparação (Moldin), e de
        // volta «Reparado» na 4152. O MF faz a mesma ida no dia seguinte.
        foreach (var (toolId, dia) in new[] { (c.Cm, 5), (c.Mf, 6) })
        {
            var armazem = Movimentos(db);
            var operacoes = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            c.Operacoes.AddRange(operacoes);
            Assert.True((await armazem.EntradaAsync(new PedidoDeEntrada(operacoes[0], toolId, HistoriaPosicao, ArmazemOrigem.JaEstava,
                FerramentaEstadoTipo.PorReparar, new DateTime(2026, 9, dia, 8, 0, 0, DateTimeKind.Utc), null, CriarPosicao: true))).Gravou);
            Assert.True((await armazem.SaidaAsync(new PedidoDeSaida(operacoes[1], toolId, ArmazemDestino.Reparacao, c.ReparadorId, null, null,
                new DateTime(2026, 9, dia, 10, 0, 0, DateTimeKind.Utc), null))).Gravou);
            Assert.True((await armazem.EntradaAsync(new PedidoDeEntrada(operacoes[2], toolId, HistoriaPosicao, ArmazemOrigem.Reparacao,
                FerramentaEstadoTipo.Reparado, new DateTime(2026, 9, dia + 2, 10, 0, 0, DateTimeKind.Utc), null))).Gravou);
            db.ChangeTracker.Clear();
        }
    }

    private async Task HistoriaRestosAsync(DmoDbContext db)
    {
        var restos = await db.JobOns.AsNoTracking()
            .Where(j => (j.Reference == "5612T173" && (j.ProductionNumber == "202601" || j.ProductionNumber == "202602"))
                || (j.Reference == "9162T173" && j.ProductionNumber == "202601"))
            .Select(j => j.Id).ToListAsync();
        foreach (var id in restos)
        {
            await new DeleteJobOnHandler(db).HandleAsync(new DeleteJobOn(id));
            db.ChangeTracker.Clear();
        }
    }

    private async Task HistoriaLimparAsync(HistoriaCenario c)
    {
        await using var db = CreateContext();
        await HistoriaRestosAsync(db);
        await db.BqMovementDeletions.Where(d => c.Traces.Contains(d.BqRepairTraceId)).ExecuteDeleteAsync();
        await db.OperacoesRegistadas.Where(o => c.Operacoes.Contains(o.Id)).ExecuteDeleteAsync();
        var tools = new[] { c.Cm, c.Mf, c.Bq, c.Bq02, c.Mf9162 };
        await db.FerramentaEstados.Where(e => tools.Contains(e.ToolId)).ExecuteDeleteAsync();
        await db.FerramentaLocalizacoes.Where(l => tools.Contains(l.ToolId)).ExecuteDeleteAsync();
        await db.ArmazemOcupacoes.Where(o => tools.Contains(o.ToolId)).ExecuteDeleteAsync();
        await db.ArmazemMovimentos.Where(m => tools.Contains(m.ToolId!)).ExecuteDeleteAsync();
        if (c.PosicaoCriada)
        {
            await db.ArmazemPosicoes.Where(p => p.Codigo == HistoriaPosicao).ExecuteDeleteAsync();
        }

        if (c.ReparadorCriado is { } reparador)
        {
            await db.Reparadores.Where(r => r.Id == reparador).ExecuteDeleteAsync();
        }

        await db.ToolTechnicalContexts.Where(t => tools.Contains(t.ToolId)).ExecuteDeleteAsync();
        await db.Tools.Where(t => tools.Contains(t.ToolId)).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == c.Quem).ExecuteDeleteAsync();
        await db.Machines.Where(m => c.MaquinasCriadas.Contains(m.Id)).ExecuteDeleteAsync();
    }
}

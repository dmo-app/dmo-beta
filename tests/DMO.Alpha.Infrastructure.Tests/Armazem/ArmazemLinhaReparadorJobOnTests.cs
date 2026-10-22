using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// Cada movimento do Armazém guarda a Linha, o reparador e o Job On a que
/// pertence: a Saída para Produção liga o Job On dessa ferramenta nessa
/// Linha; a Entrada, a ida à Reparação e o regresso levam-no com elas; a
/// Entrada vinda da Reparação guarda o reparador da última Saída (editável no
/// formulário). A história do Job On diz quem reparou cada CM e MF (as RI e as
/// idas à Reparação, pelo Job On guardado; as de antes, pelas datas), e a
/// ficha da CM mostra as RI e os Pesos dela, com o Job On e a data.
/// </summary>
public sealed class ArmazemLinhaReparadorJobOnTests
{
    private const string Cm = "cm-5447";
    private const string Mf = "mf-5447";
    private const string Bq = "bq-t173";
    private const int B1 = 1;
    private const int B2 = 2;
    private const int JobOn1 = 10;
    private const int JobOn2 = 11;
    private static readonly Guid Ana = Guid.NewGuid();

    private static DateTime Utc(int mes, int dia, int hora = 10) => new(2026, mes, dia, hora, 0, 0, DateTimeKind.Utc);

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-linha-jobon-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.Machines.AddRange(new Machine { Id = B1, Code = "B1" }, new Machine { Id = B2, Code = "B2" });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = Cm, MachineId = B1 });
        // O Job On 202601 na B1 (1 a 10 de setembro) e o 202602 a seguir (11 a 20), com a CM e o MF.
        db.JobOns.AddRange(
            new JobOn { Id = JobOn1, Reference = "5447T173", ProductionNumber = "202601", MachineId = B1, InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 10) },
            new JobOn { Id = JobOn2, Reference = "5447T173", ProductionNumber = "202602", MachineId = B1, InicioEm = new DateOnly(2026, 9, 11), FimEm = new DateOnly(2026, 9, 20) });
        db.CmContexts.AddRange(new CmContext { Id = 100, JobOnId = JobOn1, ToolId = Cm }, new CmContext { Id = 101, JobOnId = JobOn2, ToolId = Cm });
        db.MfContexts.AddRange(new MfContext { Id = 200, JobOnId = JobOn1, ToolId = Mf }, new MfContext { Id = 201, JobOnId = JobOn2, ToolId = Mf });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.AddRange(new Reparador { Id = 1, Nome = "Moldes Lda" }, new Reparador { Id = 2, Nome = "Moldin" });
        db.Users.Add(new User { Id = Ana, Name = "Ana", OperatorId = "4801", ProviderUserId = "p-ana" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemMovimentos Servico(DmoDbContext db) => new(db, new OperacoesRegistadas(db), new Sessao());

    private static async Task EntrarAsync(DmoDbContext db, string toolId, ArmazemOrigem origem, DateTime quando, FerramentaEstadoTipo estado = FerramentaEstadoTipo.Reparado)
    {
        Assert.True((await Servico(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, "0042", origem, estado, quando, null))).Gravou);
        db.ChangeTracker.Clear();
    }

    private static async Task SairAsync(DmoDbContext db, string toolId, ArmazemDestino destino, DateTime quando, int? linha = null, int? reparador = null)
    {
        Assert.True((await Servico(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), toolId, destino, reparador, null, null, quando, null, linha))).Gravou);
        db.ChangeTracker.Clear();
    }

    private static Task<ArmazemMovimento> UltimoAsync(DmoDbContext db, string toolId = Cm) =>
        db.ArmazemMovimentos.AsNoTracking().Where(m => m.ToolId == toolId).OrderByDescending(m => m.Id).FirstAsync();

    [Fact]
    public async Task SaidaParaProducao_GuardaALinhaEOJobOnDessaFerramentaNessaLinha()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));

        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 3), linha: B1);

        var saida = await UltimoAsync(db);
        Assert.Equal(((int?)B1, (int?)JobOn1), (saida.MachineId, saida.JobOnId));
    }

    [Fact]
    public async Task SaidaParaProducao_AntesDeAProducaoComecar_LigaAoProximoJobOnDaLinha()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 20));

        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(8, 31), linha: B1);

        Assert.Equal((int?)JobOn1, (await UltimoAsync(db)).JobOnId);
    }

    [Fact]
    public async Task SaidaParaProducao_NoutraLinhaSemJobOn_GuardaSoALinha()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));

        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 3), linha: B2);

        var saida = await UltimoAsync(db);
        Assert.Equal(((int?)B2, (int?)null), (saida.MachineId, saida.JobOnId));
    }

    [Fact]
    public async Task SaidaParaProducao_ComUmaLinhaQueNaoExiste_GravaSemLinha()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));

        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 3), linha: 999);

        var saida = await UltimoAsync(db);
        Assert.Equal(((int?)null, (int?)null), (saida.MachineId, saida.JobOnId));
    }

    [Fact]
    public async Task OJobOnSegueAFerramenta_DaProducaoAReparacaoEDeVolta_ComOReparadorDaSaida()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 1), linha: B1);

        await EntrarAsync(db, Cm, ArmazemOrigem.Producao, Utc(9, 10), FerramentaEstadoTipo.PorReparar);
        var vindaDaProducao = await UltimoAsync(db);
        Assert.Equal(((int?)B1, (int?)JobOn1, (string?)null), (vindaDaProducao.MachineId, vindaDaProducao.JobOnId, vindaDaProducao.EntradaReparadorNome));

        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 11), reparador: 2);
        var paraAReparacao = await UltimoAsync(db);
        Assert.Equal(((int?)B1, (int?)JobOn1, "Moldin"), (paraAReparacao.MachineId, paraAReparacao.JobOnId, paraAReparacao.ReparadorNome));

        await EntrarAsync(db, Cm, ArmazemOrigem.Reparacao, Utc(9, 14));
        var deVolta = await UltimoAsync(db);
        Assert.Equal(((int?)B1, (int?)JobOn1, "Moldin"), (deVolta.MachineId, deVolta.JobOnId, deVolta.EntradaReparadorNome));
    }

    [Fact]
    public async Task Situacao_ForaPelaReparacao_PropoeALinhaEOReparadorComQueVem()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 1), linha: B1);
        await EntrarAsync(db, Cm, ArmazemOrigem.Producao, Utc(9, 10), FerramentaEstadoTipo.PorReparar);
        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 11), reparador: 2);

        var situacao = await Servico(db).SituacaoAsync(Cm);

        Assert.NotNull(situacao);
        Assert.Equal(((int?)B1, "Moldin"), (situacao!.VindaLinhaId, situacao.VindaReparador));
        Assert.Equal(new[] { "B1", "B2" }, situacao.Linhas.Select(l => l.Codigo));
        Assert.Equal(new[] { B1 }, situacao.LinhasDaFerramenta);
    }

    [Fact]
    public async Task Entrada_ComALinhaEOReparadorDoFormulario_GuardaOQueAPessoaDeixou()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 1), reparador: 1);

        var gravou = await Servico(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.Reparacao,
            FerramentaEstadoTipo.Reparado, Utc(9, 4), null, LinhaId: B2, Reparador: "  Moldin  ", LinhaEReparadorDoFormulario: true));

        Assert.True(gravou.Gravou);
        var entrada = await UltimoAsync(db);
        Assert.Equal(((int?)B2, "Moldin"), (entrada.MachineId, entrada.EntradaReparadorNome));
    }

    [Fact]
    public async Task Entrada_ComOReparadorApagadoNoFormulario_FicaSemReparador()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 1), reparador: 1);

        Assert.True((await Servico(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.Reparacao,
            FerramentaEstadoTipo.Reparado, Utc(9, 4), null, Reparador: " ", LinhaEReparadorDoFormulario: true))).Gravou);

        var entrada = await UltimoAsync(db);
        Assert.Equal(((int?)null, (string?)null), (entrada.MachineId, entrada.EntradaReparadorNome));
    }

    [Fact]
    public async Task HistoriaDoJobOn_RIEIdasAReparacao_PeloJobOnGuardadoEPelasDatasNasDeAntes()
    {
        using var db = CreateContext();
        // A CM: a ida à Reparação com o Job On guardado (pela Saída para Produção).
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 1), linha: B1);
        await EntrarAsync(db, Cm, ArmazemOrigem.Producao, Utc(9, 5), FerramentaEstadoTipo.PorReparar);
        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 6), reparador: 2);
        await EntrarAsync(db, Cm, ArmazemOrigem.Reparacao, Utc(9, 8));
        // O MF: um movimento de antes (sem Job On), nas datas do Job On; e
        // outro fora delas, que não conta.
        db.ArmazemMovimentos.AddRange(
            new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Saida, ToolId = Mf, Destino = ArmazemDestino.Reparacao, ReparadorNome = "Moldes Lda", OcorridoEmUtc = Utc(9, 7), RegistadoEmUtc = Utc(9, 7), OperacaoId = Guid.NewGuid() },
            new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Saida, ToolId = Mf, Destino = ArmazemDestino.Reparacao, ReparadorNome = "Moldes Lda", OcorridoEmUtc = Utc(9, 15), RegistadoEmUtc = Utc(9, 15), OperacaoId = Guid.NewGuid() });
        // A RI da CM no Job On, registada pela Ana; e uma anulada, que não conta.
        db.ReparacoesInternas.AddRange(
            new Reparacao { JobOnId = JobOn1, MachineId = B1, Tipo = ReparacaoTipo.Cm, CmContextId = 100, Numero = "17", RegistadoPorUserId = Ana, RegistadoEmUtc = Utc(9, 3), DiaOperacional = new DateOnly(2026, 9, 3) },
            new Reparacao { JobOnId = JobOn1, MachineId = B1, Tipo = ReparacaoTipo.Mf, MfContextId = 200, Numero = "8", RegistadoPorUserId = Ana, RegistadoEmUtc = Utc(9, 4), DiaOperacional = new DateOnly(2026, 9, 4), AnuladaEmUtc = Utc(9, 4, 12) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var historia = await new HistoriaDasReparacoes(db).DoJobOnAsync(JobOn1);

        Assert.Equal(3, historia.Count);
        var ri = Assert.Single(historia, h => h.Origem == QuemReparouOrigem.ReparacaoInterna);
        Assert.Equal((Cm, ToolType.Cm, "Ana", "17", Utc(9, 3)), (ri.ToolId, ri.Tipo, ri.Reparador, ri.Numero, ri.EmUtc));
        var cm = Assert.Single(historia, h => h.Origem == QuemReparouOrigem.Armazem && h.ToolId == Cm);
        Assert.Equal(("Moldin", Utc(9, 6), (DateTime?)Utc(9, 8), false), (cm.Reparador, cm.EmUtc, cm.RegressoEmUtc, cm.PelaData));
        var mf = Assert.Single(historia, h => h.Origem == QuemReparouOrigem.Armazem && h.ToolId == Mf);
        Assert.Equal(("Moldes Lda", Utc(9, 7), (DateTime?)null, true), (mf.Reparador, mf.EmUtc, mf.RegressoEmUtc, mf.PelaData));

        // O Job On seguinte não fica com a ida guardada no primeiro.
        var segundo = await new HistoriaDasReparacoes(db).DoJobOnAsync(JobOn2);
        var soOMf = Assert.Single(segundo);
        Assert.Equal((Mf, Utc(9, 15)), (soOMf.ToolId, soOMf.EmUtc));
        Assert.Empty(await new HistoriaDasReparacoes(db).DoJobOnAsync(999));
    }

    [Fact]
    public async Task HistoriaDoJobOn_OReparadorCorrigidoNoRegresso_Manda()
    {
        using var db = CreateContext();
        await EntrarAsync(db, Cm, ArmazemOrigem.JaEstava, Utc(8, 30));
        await SairAsync(db, Cm, ArmazemDestino.Producao, Utc(9, 1), linha: B1);
        await EntrarAsync(db, Cm, ArmazemOrigem.Producao, Utc(9, 5), FerramentaEstadoTipo.PorReparar);
        await SairAsync(db, Cm, ArmazemDestino.Reparacao, Utc(9, 6), reparador: 1);
        Assert.True((await Servico(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.Reparacao,
            FerramentaEstadoTipo.Reparado, Utc(9, 8), null, LinhaId: B1, Reparador: "Moldin", LinhaEReparadorDoFormulario: true))).Gravou);

        var ida = Assert.Single(await new HistoriaDasReparacoes(db).DoJobOnAsync(JobOn1));

        Assert.Equal("Moldin", ida.Reparador);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.ReparacaoInterna;

/// <summary>
/// A Reparação Interna, peça 4 (Diogo, 21/10/2026): a CM ou o MF trocado na
/// máquina antes de a troca chegar ao Job On, e a máquina sem produção para o
/// dia. Grava-se a referência e o lote escritos (sem espaços nas pontas, a
/// referência em maiúsculas, até 40 e 12), sem contexto, e sem Job On quando
/// não há; o responsável associa depois a ferramenta do mesmo tipo, com
/// rasto, uma vez por envio. A Consulta mostra as por associar, com as
/// ferramentas que servem (as do lote primeiro; sem nenhuma, as da referência).
/// </summary>
public sealed class ReparacoesInternasOutraFerramentaTests
{
    // 10:00 em Lisboa (inverno, UTC+0): o dia operacional é 11/03.
    private static readonly DateTimeOffset Manha = new(2030, 3, 11, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Dia = new(2030, 3, 11);

    private static readonly Guid Rui = Guid.Parse("8f0c4c2e-1d1b-4b8e-9a52-0f6d6f1a0b04");

    private static readonly Guid Chefe = Guid.Parse("8f0c4c2e-1d1b-4b8e-9a52-0f6d6f1a0b05");

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-ri-outra-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 1, Code = "B1" }, new Machine { Id = 3, Code = "C1" });
        db.Users.AddRange(
            new User { Id = Rui, Name = "Rui Costa", OperatorId = "2165", ProviderUserId = "p-2165" },
            new User { Id = Chefe, Name = "Ana Chefe", OperatorId = "2166", ProviderUserId = "p-2166" });
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "cm-b", Type = ToolType.Cm, Reference = "6012A118", Lot = "4" },
            new Tool { ToolId = "mf-b", Type = ToolType.Mf, Reference = "6012A118", Lot = "4" },
            // A CM que estava na máquina: a 5447T173 nos lotes 3 e 4; o MF dela só no lote 3.
            new Tool { ToolId = "cm-x4", Type = ToolType.Cm, Reference = "5447T173", Lot = "4" },
            new Tool { ToolId = "cm-x3", Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "mf-x3", Type = ToolType.Mf, Reference = "5447T173", Lot = "3" });
        db.JobOns.AddRange(
            new JobOn { Id = 11, MachineId = 1, Reference = "6012A118", ProductionNumber = "202511", InicioEm = Dia, FimEm = null },
            // C1: só uma sem datas, não conta.
            new JobOn { Id = 15, MachineId = 3, Reference = "6012A118", ProductionNumber = "202515" });
        db.CmContexts.Add(new CmContext { Id = 101, JobOnId = 11, ToolId = "cm-b" });
        db.MfContexts.Add(new MfContext { Id = 201, JobOnId = 11, ToolId = "mf-b" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ReparacoesInternas Servico(DmoDbContext db, Guid? quem = null, DateTimeOffset? agora = null) =>
        new(db, new SemHora(), new OperacoesRegistadas(db), new Sessao(quem ?? Rui), new Relogio(agora ?? Manha));

    private static PedidoDeReparacao Outra(string? numero, string? referencia, string? lote, ReparacaoTipo tipo = ReparacaoTipo.Cm, int maquina = 1, int? jobOn = 11) =>
        new(Guid.NewGuid(), maquina, jobOn, tipo, tipo == ReparacaoTipo.Cm ? 101 : 201, numero, true, referencia, lote);

    private static async Task<int> RegistarAsync(DmoDbContext db, string numero, string referencia = "5447T173", string lote = "4", ReparacaoTipo tipo = ReparacaoTipo.Cm) =>
        (await Servico(db).RegistarAsync(Outra(numero, referencia, lote, tipo))).ReparacaoId!.Value;

    [Fact]
    public async Task OutraCm_GravaAReferenciaEOLoteEscritos_SemContexto_ComOJobOn()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(Outra(" 0457 ", "  5447t173 ", " 4  "));

        Assert.Equal((ReparacaoResultado.Gravado, "0457", "5447T173", "4", false, true),
            (saida.Resultado, saida.Numero, saida.Referencia, saida.Lote, saida.SemJobOn, saida.PorAssociar));
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(((int?)11, 1, ReparacaoTipo.Cm, (int?)null, (int?)null, "0457"), (r.JobOnId, r.MachineId, r.Tipo, r.CmContextId, r.MfContextId, r.Numero));
        Assert.Equal(("5447T173", "4", (string?)null), (r.ReferenciaEscrita, r.LoteEscrito, r.AssociadaToolId));
        Assert.Equal(((Guid?)Rui, Manha.UtcDateTime, Dia), (r.RegistadoPorUserId, r.RegistadoEmUtc, r.DiaOperacional));
        Assert.True(r.PorAssociar);
        // Na tabela do registo: a escrita, curta.
        var linha = Assert.Single((await Servico(db).ListarAsync(1, 11, null)).Items);
        Assert.Equal(("5447T173", "4"), (linha.Referencia, linha.Lote));
    }

    [Fact]
    public async Task SemOutra_ARegraDoJobOnFicaIgual_AReferenciaEscritaNaoConta()
    {
        using var db = CreateContext();

        await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), 1, 11, ReparacaoTipo.Mf, 201, "5", false, "5447T173", "4"));

        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(((int?)201, (string?)null, (string?)null), (r.MfContextId, r.ReferenciaEscrita, r.LoteEscrito));
    }

    [Theory]
    [InlineData(39, ReparacaoResultado.Gravado)]
    [InlineData(40, ReparacaoResultado.Gravado)]
    [InlineData(41, ReparacaoResultado.ReferenciaLonga)]
    public async Task AReferencia_TemAte40_SemContarOsEspacosDasPontas(int tamanho, ReparacaoResultado esperado)
    {
        using var db = CreateContext();
        var referencia = new string('R', tamanho);

        var saida = await Servico(db).RegistarAsync(Outra("1", "  " + referencia + " ", "4"));

        Assert.Equal((esperado, "1"), (saida.Resultado, saida.Numero));
        Assert.Equal(esperado == ReparacaoResultado.Gravado ? [referencia] : Array.Empty<string>(),
            await db.ReparacoesInternas.Select(r => r.ReferenciaEscrita!).ToListAsync());
    }

    [Theory]
    [InlineData(11, ReparacaoResultado.Gravado)]
    [InlineData(12, ReparacaoResultado.Gravado)]
    [InlineData(13, ReparacaoResultado.LoteLongo)]
    public async Task OLote_TemAte12_SemContarOsEspacosDasPontas(int tamanho, ReparacaoResultado esperado)
    {
        using var db = CreateContext();
        var lote = new string('4', tamanho);

        var saida = await Servico(db).RegistarAsync(Outra("1", "5447T173", " " + lote + "  "));

        Assert.Equal(esperado, saida.Resultado);
        Assert.Equal(esperado == ReparacaoResultado.Gravado ? [lote] : Array.Empty<string>(),
            await db.ReparacoesInternas.Select(r => r.LoteEscrito!).ToListAsync());
    }

    [Theory]
    [InlineData(null, "4", ReparacaoResultado.SemReferencia)]
    [InlineData("  ", "4", ReparacaoResultado.SemReferencia)]
    [InlineData("5447T173", null, ReparacaoResultado.SemLote)]
    [InlineData("5447T173", " ", ReparacaoResultado.SemLote)]
    public async Task OutraSemAReferenciaOuOLote_Recusa_ComONumeroEscrito_ENadaSeGrava(string? referencia, string? lote, ReparacaoResultado esperado)
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(Outra("77", referencia, lote));

        Assert.Equal((esperado, "77", false), (saida.Resultado, saida.Numero, saida.Gravou));
        Assert.Empty(await db.ReparacoesInternas.ToListAsync());
        Assert.Empty(await db.OperacoesRegistadas.ToListAsync());
    }

    [Fact]
    public async Task SemProducao_GravaSemJobOn_ComAEscrita_EAListaDaMaquinaNoDia()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), 3, null, ReparacaoTipo.Mf, null, "9", false, "5447t173", "3"));

        Assert.Equal((ReparacaoResultado.Gravado, true, "5447T173"), (saida.Resultado, saida.SemJobOn, saida.Referencia));
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(((int?)null, 3, ReparacaoTipo.Mf, (int?)null, (int?)null, "3"), (r.JobOnId, r.MachineId, r.Tipo, r.CmContextId, r.MfContextId, r.LoteEscrito));
        Assert.Equal("9", Assert.Single((await Servico(db).ListarSemProducaoAsync(3, Dia, null)).Items).Numero);
        Assert.Empty((await Servico(db).ListarSemProducaoAsync(3, Dia.AddDays(1), null)).Items);
        Assert.Empty((await Servico(db).ListarSemProducaoAsync(1, Dia, null)).Items);
    }

    [Fact]
    public async Task APaginaMostrouUmaProducaoQueJaNaoConta_Mudou()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(Outra("9", "5447T173", "3", maquina: 3, jobOn: 15));

        Assert.Equal((ReparacaoResultado.ProducaoMudou, "9"), (saida.Resultado, saida.Numero));
        Assert.Empty(await db.ReparacoesInternas.ToListAsync());
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_GravaUmaVez_EVoltaComAEscrita()
    {
        using var db = CreateContext();
        var pedido = Outra("88", "5447T173", "4");

        var primeira = await Servico(db).RegistarAsync(pedido);
        var repetida = await Servico(db).RegistarAsync(pedido);

        Assert.Equal(ReparacaoResultado.Gravado, primeira.Resultado);
        Assert.Equal((ReparacaoResultado.JaRegistado, primeira.ReparacaoId, "5447T173", "4", true),
            (repetida.Resultado, repetida.ReparacaoId, repetida.Referencia, repetida.Lote, repetida.PorAssociar));
        Assert.Single(await db.ReparacoesInternas.ToListAsync());
    }

    [Fact]
    public async Task Associar_AFerramentaDoTipo_GravaQuemEQuando_EORasto()
    {
        using var db = CreateContext();
        var id = await RegistarAsync(db, "0457");
        var envio = Guid.NewGuid();

        var saida = await Servico(db, Chefe, Manha.AddHours(2)).AssociarAsync(new PedidoDeAssociacao(envio, id, " cm-x4 "));

        Assert.Equal((AssociacaoResultado.Associado, "0457", "5447T173 · lote 4"), (saida.Resultado, saida.Numero, saida.Ferramenta));
        var r = await db.ReparacoesInternas.AsNoTracking().SingleAsync();
        Assert.Equal(("cm-x4", (Guid?)Chefe, (DateTime?)Manha.UtcDateTime.AddHours(2)), (r.AssociadaToolId, r.AssociadaPorUserId, r.AssociadaEmUtc));
        Assert.False(r.PorAssociar);
        // A escrita fica como foi escrita; o contexto continua vazio.
        Assert.Equal(("5447T173", "4", (int?)null), (r.ReferenciaEscrita, r.LoteEscrito, r.CmContextId));
        var rasto = await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync();
        Assert.Equal((ReparacaoAcao.Associado, "0457", (string?)null, "cm-x4", (Guid?)Chefe, (string?)null),
            (rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.ToolIdAssociado, rasto.PorUserId, rasto.Motivo));
        var operacao = await db.OperacoesRegistadas.AsNoTracking().SingleAsync(o => o.Id == envio);
        Assert.Equal((ReparacoesInternas.OperacaoAssociar, (int?)id), (operacao.Tipo, operacao.ResultadoId));
    }

    [Fact]
    public async Task Associar_OMesmoEnvioDuasVezes_AssociaUmaVez()
    {
        using var db = CreateContext();
        var id = await RegistarAsync(db, "1");
        var pedido = new PedidoDeAssociacao(Guid.NewGuid(), id, "cm-x4");

        var primeira = await Servico(db, Chefe).AssociarAsync(pedido);
        var repetida = await Servico(db, Chefe).AssociarAsync(pedido);

        Assert.Equal(AssociacaoResultado.Associado, primeira.Resultado);
        Assert.Equal((AssociacaoResultado.JaFeito, "1", "5447T173 · lote 4", true), (repetida.Resultado, repetida.Numero, repetida.Ferramenta, repetida.Gravou));
        Assert.Single(await db.ReparacoesInternasRasto.ToListAsync());
    }

    [Fact]
    public async Task Associar_OQueNaoServe_Recusa_ENadaMuda()
    {
        using var db = CreateContext();
        var cm = await RegistarAsync(db, "1");
        var mf = await RegistarAsync(db, "2", lote: "3", tipo: ReparacaoTipo.Mf);
        var doJobOn = (await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), 1, 11, ReparacaoTipo.Cm, 101, "3"))).ReparacaoId!.Value;
        var anulada = await RegistarAsync(db, "4");
        await Servico(db).AnularAsync(new PedidoDeAnulacao(Guid.NewGuid(), anulada));
        var rastoAntes = await db.ReparacoesInternasRasto.CountAsync();

        async Task<AssociacaoResultado> Associar(int id, string? toolId) =>
            (await Servico(db, Chefe).AssociarAsync(new PedidoDeAssociacao(Guid.NewGuid(), id, toolId))).Resultado;

        // Um MF para uma CM, uma CM para um MF, a REMOVED e uma que não existe.
        Assert.Equal(AssociacaoResultado.FerramentaNaoServe, await Associar(cm, "mf-x3"));
        Assert.Equal(AssociacaoResultado.FerramentaNaoServe, await Associar(mf, "cm-x3"));
        Assert.Equal(AssociacaoResultado.FerramentaNaoServe, await Associar(cm, ToolRegistryConstants.RemovedToolId));
        Assert.Equal(AssociacaoResultado.FerramentaNaoServe, await Associar(cm, "nao-existe"));
        Assert.Equal(AssociacaoResultado.SemFerramenta, await Associar(cm, " "));
        Assert.Equal(AssociacaoResultado.NaoEscrita, await Associar(doJobOn, "cm-b"));
        Assert.Equal(AssociacaoResultado.JaAnulada, await Associar(anulada, "cm-x4"));
        Assert.Equal(AssociacaoResultado.NaoExiste, await Associar(9999, "cm-x4"));
        Assert.Equal(rastoAntes, await db.ReparacoesInternasRasto.CountAsync());
        Assert.All(await db.ReparacoesInternas.AsNoTracking().ToListAsync(), r => Assert.Null(r.AssociadaToolId));

        // O MF associa-se a um MF; outro envio depois diz que já estava associada, a qual.
        Assert.Equal(AssociacaoResultado.Associado, await Associar(mf, "mf-x3"));
        var outra = await Servico(db, Rui).AssociarAsync(new PedidoDeAssociacao(Guid.NewGuid(), mf, "mf-b"));
        Assert.Equal((AssociacaoResultado.JaAssociada, "5447T173 · lote 3"), (outra.Resultado, outra.Ferramenta));
        Assert.Equal("mf-x3", (await db.ReparacoesInternas.AsNoTracking().SingleAsync(r => r.Id == mf)).AssociadaToolId);
    }

    [Fact]
    public async Task Consulta_SoPorAssociar_ComAsFerramentasQueServem()
    {
        using var db = CreateContext();
        var doLote = await RegistarAsync(db, "1");
        var daReferencia = await RegistarAsync(db, "2", lote: "9");
        var nenhuma = await RegistarAsync(db, "3", referencia: "9999Z999");
        var associada = await RegistarAsync(db, "4", lote: "3");
        await Servico(db, Chefe).AssociarAsync(new PedidoDeAssociacao(Guid.NewGuid(), associada, "cm-x3"));
        var semJobOn = (await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), 3, null, ReparacaoTipo.Mf, null, "5", false, "5447T173", "3"))).ReparacaoId!.Value;
        await Servico(db).RegistarAsync(new PedidoDeReparacao(Guid.NewGuid(), 1, 11, ReparacaoTipo.Cm, 101, "6"));
        var anulada = await RegistarAsync(db, "7");
        await Servico(db).AnularAsync(new PedidoDeAnulacao(Guid.NewGuid(), anulada));
        var consulta = new ReparacoesInternasConsulta(db);

        var porAssociar = await consulta.ConsultarAsync(new RiConsultaFiltro(PorAssociar: true), null);

        Assert.True(new RiConsultaFiltro(PorAssociar: true).ComFiltro);
        Assert.Equal(new[] { semJobOn, nenhuma, daReferencia, doLote }, porAssociar.Items.Select(l => l.Id));
        var linhas = porAssociar.Items.ToDictionary(l => l.Id);
        // O lote escrito existe: só essa; senão as da referência; senão nenhuma.
        Assert.Equal(new[] { "cm-x4" }, linhas[doLote].Escrita!.Candidatas!.Select(c => c.ToolId));
        Assert.True(linhas[doLote].Escrita!.DoLote);
        Assert.Equal(new[] { "cm-x3", "cm-x4" }, linhas[daReferencia].Escrita!.Candidatas!.Select(c => c.ToolId));
        Assert.False(linhas[daReferencia].Escrita!.DoLote);
        Assert.Empty(linhas[nenhuma].Escrita!.Candidatas!);
        // Só as do tipo: o MF escrito tem o MF.
        Assert.Equal(new[] { new RiCandidata("mf-x3", "5447T173", "3") }, linhas[semJobOn].Escrita!.Candidatas!);
        // Sem Job On: sem produção.
        Assert.Equal(((int?)null, (string?)null, (string?)null, "C1"), (linhas[semJobOn].JobOnId, linhas[semJobOn].Producao, linhas[semJobOn].Referencia, linhas[semJobOn].Maquina));
        Assert.True(linhas[doLote].Escrita!.PorAssociar);
        Assert.Null(linhas[doLote].Ferramenta);

        // Sem o filtro, a associada mostra a ferramenta associada e não leva candidatas.
        var todas = await consulta.ConsultarAsync(new RiConsultaFiltro(MachineId: 1), null);
        var feita = todas.Items.Single(l => l.Id == associada).Escrita!;
        Assert.Equal(("cm-x3", "5447T173", "3", false), (feita.AssociadaToolId, feita.AssociadaReferencia, feita.AssociadaLote, feita.PorAssociar));
        Assert.Null(feita.Candidatas);
        Assert.Null(todas.Items.Single(l => l.Numero == "6").Escrita);
    }

    [Fact]
    public async Task EliminarAFerramentaAssociada_PassaParaAREMOVED_EORastoFica()
    {
        using var db = CreateContext();
        var id = await RegistarAsync(db, "1");
        await Servico(db, Chefe).AssociarAsync(new PedidoDeAssociacao(Guid.NewGuid(), id, "cm-x4"));

        Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool("cm-x4"))).Result);

        Assert.Equal(ToolRegistryConstants.RemovedToolId, (await db.ReparacoesInternas.AsNoTracking().SingleAsync()).AssociadaToolId);
        Assert.Equal("cm-x4", (await db.ReparacoesInternasRasto.AsNoTracking().SingleAsync()).ToolIdAssociado);
        var linha = Assert.Single((await new ReparacoesInternasConsulta(db).ConsultarAsync(new RiConsultaFiltro(MachineId: 1), null)).Items);
        Assert.Equal((false, (string?)null), (linha.Escrita!.PorAssociar, linha.Escrita.AssociadaReferencia));
    }

    private sealed class SemHora : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class Relogio(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class Sessao(Guid quem) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = quem, DisplayName = "Pessoa", OperatorId = "2165" };
    }
}

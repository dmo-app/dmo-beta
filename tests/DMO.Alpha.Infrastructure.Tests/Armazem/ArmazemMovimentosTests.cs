using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// Os movimentos do Armazém (0.2.0-beta.9): a Entrada põe a ferramenta numa
/// posição, a Saída tira-a para Reparação, Produção, Emprestado ou Sucata, e
/// Corrigir localização emenda um engano; cada um grava o movimento, a
/// ocupação, a localização e o estado escolhido de uma vez, e o mesmo envio
/// duas vezes grava uma. Só se recusa o impossível ou o que falta: a data no
/// futuro ou anterior, a posição arquivada ou com outra ferramenta e a
/// entrada depois da Sucata gravam e avisam. A Entrada propõe a origem e o
/// estado pela última Saída (D11).
/// </summary>
public sealed class ArmazemMovimentosTests
{
    private const string Cm = "cm-5447";
    private const string Mf = "mf-5447";
    private const string Bq = "bq-t173";

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-movimentos-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" },
            new Tool { ToolId = "cm-1075", Type = ToolType.Cm, Reference = "1075C142", Lot = "2" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Id = 2, Codigo = "0043", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Id = 3, Codigo = "0099", Ativa = false, CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.AddRange(
            new Reparador { Id = 1, Nome = "Moldes Lda" },
            new Reparador { Id = 2, Nome = "Só MF", AplicaCm = false },
            new Reparador { Id = 3, Nome = "Arquivado", Ativo = false });
        db.BqRepairers.Add(new BqRepairer { Id = 7, Name = "LE" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemMovimentos Servico(DmoDbContext db) =>
        new(db, new OperacoesRegistadas(db), new Sessao());

    private static PedidoDeEntrada Entrada(string toolId = Cm, string? posicao = "0042", DateTime? data = null, string? observacoes = null, bool criar = false) =>
        new(Guid.NewGuid(), toolId, posicao, ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, data, observacoes, criar);

    private static PedidoDeSaida Saida(ArmazemDestino? destino, string toolId = Cm, int? reparador = null, string? fabrica = null,
        FerramentaEstadoTipo? estado = null, DateTime? data = null) =>
        new(Guid.NewGuid(), toolId, destino, reparador, fabrica, estado, data, null);

    [Theory]
    [InlineData("entrada")]
    [InlineData("saida")]
    [InlineData("corrigir")]
    public async Task UmaFerramentaQueNaoExiste_RecusaENaoGravaNada(string movimento)
    {
        using var db = CreateContext();
        var servico = Servico(db);

        var saida = movimento switch
        {
            "entrada" => await servico.EntradaAsync(Entrada(toolId: "nao-existe")),
            "saida" => await servico.SaidaAsync(Saida(ArmazemDestino.Producao, toolId: "nao-existe")),
            _ => await servico.CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), "nao-existe", "0042", null))
        };

        Assert.Equal(ArmazemMovimentoResultado.FerramentaNaoExiste, saida.Resultado);
        Assert.Empty(await db.ArmazemMovimentos.ToListAsync());
        Assert.Empty(await db.ArmazemOcupacoes.ToListAsync());
        Assert.Empty(await db.OperacoesRegistadas.ToListAsync());
    }

    [Fact]
    public async Task Entrada_FerramentaFora_GravaMovimentoOcupacaoLocalizacaoEstadoEOperacao()
    {
        using var db = CreateContext();
        var pedido = Entrada(observacoes: "  prateleira de cima  ");

        var saida = await Servico(db).EntradaAsync(pedido);

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Equal("0042", saida.Posicao);
        Assert.Empty(saida.Avisos);
        var movimento = await db.ArmazemMovimentos.AsNoTracking().SingleAsync();
        Assert.Equal((ArmazemMovimentoTipo.Entrada, Cm, 1, ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado),
            (movimento.Tipo, movimento.ToolId, movimento.PosicaoId!.Value, movimento.Origem!.Value, movimento.Estado!.Value));
        Assert.Equal("prateleira de cima", movimento.Observacoes);
        Assert.Equal("Rui", movimento.RegistadoPor);
        Assert.Equal(pedido.OperacaoId, movimento.OperacaoId);
        Assert.False(string.IsNullOrEmpty(movimento.VersaoApp));
        var ocupacao = await db.ArmazemOcupacoes.AsNoTracking().SingleAsync();
        Assert.Equal((Cm, 1, movimento.Id, (DateTime?)null), (ocupacao.ToolId, ocupacao.PosicaoId, ocupacao.MovimentoId, ocupacao.LibertadaEmUtc));
        var localizacao = await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync();
        Assert.Equal((FerramentaLocalizacaoTipo.Fabrico, movimento.Id), (localizacao.Localizacao, localizacao.MovimentoId));
        var estado = await db.FerramentaEstados.AsNoTracking().SingleAsync();
        Assert.Equal((FerramentaEstadoTipo.Reparado, FerramentaEstado.OrigemArmazem, movimento.Id, "Rui"),
            (estado.Estado, estado.Origem, estado.MovimentoId, estado.RegistadoPor));
        var operacao = await db.OperacoesRegistadas.AsNoTracking().SingleAsync();
        Assert.Equal((pedido.OperacaoId, ArmazemMovimentos.OperacaoTipo, (int?)movimento.Id), (operacao.Id, operacao.Tipo, operacao.ResultadoId));
    }

    [Fact]
    public async Task Entrada_OMesmoEnvioDuasVezes_GravaUmaVez()
    {
        using var db = CreateContext();
        var pedido = Entrada();

        var primeira = await Servico(db).EntradaAsync(pedido);
        var segunda = await Servico(db).EntradaAsync(pedido);

        Assert.Equal(ArmazemMovimentoResultado.Gravado, primeira.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.JaRegistado, segunda.Resultado);
        Assert.Equal(primeira.MovimentoId, segunda.MovimentoId);
        Assert.Equal(1, await db.ArmazemMovimentos.CountAsync());
    }

    [Fact]
    public async Task Entrada_JaEstaNoArmazem_RecusaEDizAPosicao()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var saida = await Servico(db).EntradaAsync(Entrada(posicao: "0043"));

        Assert.Equal((ArmazemMovimentoResultado.JaNoArmazem, "0042"), (saida.Resultado, saida.Posicao));
        Assert.Equal(1, await db.ArmazemMovimentos.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00-42")]
    [InlineData("12345678901234567")]
    public async Task Entrada_PosicaoForaDaRegra_Recusa(string? posicao)
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(posicao: posicao));

        Assert.Equal(ArmazemMovimentoResultado.PosicaoInvalida, saida.Resultado);
        Assert.Empty(await db.ArmazemMovimentos.ToListAsync());
    }

    [Fact]
    public async Task Entrada_PosicaoQueNaoExiste_PedeParaCriarECriaComCriarERegistar()
    {
        using var db = CreateContext();

        var semCriar = await Servico(db).EntradaAsync(Entrada(posicao: " 7001 "));
        var comCriar = await Servico(db).EntradaAsync(Entrada(posicao: " 7001 ", criar: true));

        Assert.Equal((ArmazemMovimentoResultado.PosicaoNaoExiste, "7001"), (semCriar.Resultado, semCriar.Posicao));
        Assert.Equal((ArmazemMovimentoResultado.Gravado, "7001"), (comCriar.Resultado, comCriar.Posicao));
        var criada = await db.ArmazemPosicoes.AsNoTracking().SingleAsync(p => p.Codigo == "7001");
        Assert.Equal("Rui", criada.CriadaPor);
        Assert.Equal(criada.Id, (await db.ArmazemOcupacoes.AsNoTracking().SingleAsync()).PosicaoId);
    }

    [Fact]
    public async Task Entrada_SemOrigemOuSemEstado_Recusa()
    {
        using var db = CreateContext();

        var semOrigem = await Servico(db).EntradaAsync(Entrada() with { Origem = null });
        var semEstado = await Servico(db).EntradaAsync(Entrada() with { Estado = null });

        Assert.Equal(ArmazemMovimentoResultado.SemOrigem, semOrigem.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.SemEstado, semEstado.Resultado);
        Assert.Empty(await db.ArmazemMovimentos.ToListAsync());
    }

    [Theory]
    [InlineData(ArmazemMovimento.ObservacoesMaxLength - 1, true)]
    [InlineData(ArmazemMovimento.ObservacoesMaxLength, true)]
    [InlineData(ArmazemMovimento.ObservacoesMaxLength + 1, false)]
    public async Task Entrada_ObservacoesNoLimite_GravaAteAo200(int tamanho, bool grava)
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(observacoes: new string('x', tamanho)));

        Assert.Equal(grava ? ArmazemMovimentoResultado.Gravado : ArmazemMovimentoResultado.ObservacoesLongas, saida.Resultado);
        Assert.Equal(grava ? 1 : 0, await db.ArmazemMovimentos.CountAsync());
    }

    [Fact]
    public async Task Entrada_PosicaoArquivada_GravaEAvisa()
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(posicao: "0099"));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Contains(saida.Avisos, a => a.Contains("0099 está arquivada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Entrada_PosicaoComOutraFerramenta_GravaEAvisaQuemLaEsta()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: "cm-1075"));

        var saida = await Servico(db).EntradaAsync(Entrada());

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Contains("A posição 0042 já tem CM 1075C142 · lote 2.", saida.Avisos);
        Assert.Equal(2, await db.ArmazemOcupacoes.CountAsync(o => o.PosicaoId == 1 && o.LibertadaEmUtc == null));
    }

    [Fact]
    public async Task Entrada_OMfDaMesmaReferenciaELoteDaCm_NaoAvisa()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));

        var saida = await Servico(db).EntradaAsync(Entrada(toolId: Mf));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Empty(saida.Avisos);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(6, true)]
    [InlineData(60 * 24, true)]
    public async Task Entrada_DataNoFuturo_GravaEAvisaPassadosCincoMinutos(int minutos, bool avisa)
    {
        // Uma data até cinco minutos à frente é o relógio do PC; a seguir avisa.
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(data: DateTime.UtcNow.AddMinutes(minutos)));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Equal(avisa, saida.Avisos.Any(a => a.Contains("é no futuro", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Saida_ComDataAnteriorAoUltimoMovimento_GravaEAvisa()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(data: DateTime.UtcNow.AddDays(-1)));

        var saida = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Producao, data: DateTime.UtcNow.AddDays(-2)));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Contains(saida.Avisos, a => a.Contains("anterior ao último movimento", StringComparison.Ordinal));
        Assert.Null(await db.ArmazemOcupacoes.Where(o => o.LibertadaEmUtc == null).FirstOrDefaultAsync());
    }

    [Fact]
    public async Task Saida_ParaReparacaoDeUmaCm_UsaOsReparadoresDoArmazemECongelaONome()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var saida = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, reparador: 1));

        Assert.Equal((ArmazemMovimentoResultado.Gravado, "0042"), (saida.Resultado, saida.Posicao));
        var movimento = await db.ArmazemMovimentos.AsNoTracking().SingleAsync(m => m.Tipo == ArmazemMovimentoTipo.Saida);
        Assert.Equal((1, (int?)1, (int?)null, "Moldes Lda", (int?)null),
            (movimento.PosicaoAnteriorId!.Value, movimento.ReparadorId, movimento.BqReparadorId, movimento.ReparadorNome, movimento.PosicaoId));
        var ocupacao = await db.ArmazemOcupacoes.AsNoTracking().SingleAsync();
        Assert.Equal(movimento.Id, ocupacao.LibertadaPorMovimentoId);
        Assert.NotNull(ocupacao.LibertadaEmUtc);
        var localizacao = await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync();
        Assert.Equal((FerramentaLocalizacaoTipo.AReparar, "Moldes Lda"), (localizacao.Localizacao, localizacao.ReparadorNome));
    }

    [Fact]
    public async Task Saida_ParaReparacaoDeUmaBq_UsaOsReparadoresDasBoquilhas()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Bq));

        var doArmazem = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, toolId: Bq, reparador: 1));
        var dasBoquilhas = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, toolId: Bq, reparador: 7));

        Assert.Equal(ArmazemMovimentoResultado.ReparadorInvalido, doArmazem.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.Gravado, dasBoquilhas.Resultado);
        var movimento = await db.ArmazemMovimentos.AsNoTracking().SingleAsync(m => m.Tipo == ArmazemMovimentoTipo.Saida);
        Assert.Equal(((int?)null, (int?)7, "LE"), (movimento.ReparadorId, movimento.BqReparadorId, movimento.ReparadorNome));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(999)]
    public async Task Saida_ReparadorDeOutraFamiliaArquivadoOuQueNaoExiste_Recusa(int reparador)
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var saida = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, reparador: reparador));

        Assert.Equal(ArmazemMovimentoResultado.ReparadorInvalido, saida.Resultado);
        Assert.Equal(1, await db.ArmazemMovimentos.CountAsync());
    }

    [Fact]
    public async Task Saida_SemDestinoSemReparadorOuForaDoArmazem_Recusa()
    {
        using var db = CreateContext();
        var fora = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Producao));
        await Servico(db).EntradaAsync(Entrada());

        var semDestino = await Servico(db).SaidaAsync(Saida(null));
        var semReparador = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao));

        Assert.Equal(ArmazemMovimentoResultado.NaoEstaNoArmazem, fora.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.SemDestino, semDestino.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.SemReparador, semReparador.Resultado);
        Assert.Equal(1, await db.ArmazemMovimentos.CountAsync());
    }

    [Theory]
    [InlineData(ArmazemMovimento.FabricaMaxLength - 1, true)]
    [InlineData(ArmazemMovimento.FabricaMaxLength, true)]
    [InlineData(ArmazemMovimento.FabricaMaxLength + 1, false)]
    public async Task Saida_EmprestadoComAFabricaNoLimite_GravaAteAos60(int tamanho, bool grava)
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var saida = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Emprestado, fabrica: new string('L', tamanho)));

        Assert.Equal(grava ? ArmazemMovimentoResultado.Gravado : ArmazemMovimentoResultado.FabricaLonga, saida.Resultado);
        if (grava)
        {
            Assert.Equal(tamanho, (await db.ArmazemMovimentos.AsNoTracking().SingleAsync(m => m.Tipo == ArmazemMovimentoTipo.Saida)).FabricaDestino!.Length);
            Assert.Equal(FerramentaLocalizacaoTipo.Emprestado, (await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync()).Localizacao);
        }
    }

    [Fact]
    public async Task Saida_ParaSucata_GravaOEstadoQueAPessoaConfirmou()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Sucata, estado: FerramentaEstadoTipo.Sucatado));

        Assert.Equal(FerramentaLocalizacaoTipo.Sucata, (await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync()).Localizacao);
        Assert.Equal(FerramentaEstadoTipo.Sucatado, (await db.FerramentaEstados.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync()).Estado);
    }

    [Fact]
    public async Task Saida_ParaSucataSemEstadoEParaProducaoComEstado_NaoMudaOEstado()
    {
        // Só a escolha da pessoa muda o estado, e só na Sucata (D11).
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));
        await Servico(db).EntradaAsync(Entrada(toolId: Mf));

        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Sucata, toolId: Cm, estado: null));
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Producao, toolId: Mf, estado: FerramentaEstadoTipo.Sucatado));

        Assert.Equal(2, await db.FerramentaEstados.CountAsync());
        Assert.All(await db.FerramentaEstados.AsNoTracking().ToListAsync(), e => Assert.Equal(FerramentaEstadoTipo.Reparado, e.Estado));
        Assert.Equal(FerramentaLocalizacaoTipo.Fabrico, (await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync(l => l.ToolId == Mf)).Localizacao);
    }

    [Fact]
    public async Task Entrada_DepoisDaSucata_GravaEAvisa()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Sucata, estado: FerramentaEstadoTipo.Sucatado));

        var saida = await Servico(db).EntradaAsync(Entrada());

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Contains(saida.Avisos, a => a.Contains("estava na Sucata", StringComparison.Ordinal));
        Assert.Equal(FerramentaLocalizacaoTipo.Fabrico, (await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync()).Localizacao);
    }

    [Theory]
    [InlineData(ArmazemDestino.Producao, ArmazemOrigem.Producao, FerramentaEstadoTipo.PorReparar)]
    [InlineData(ArmazemDestino.Reparacao, ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado)]
    [InlineData(ArmazemDestino.Emprestado, ArmazemOrigem.Emprestimo, null)]
    [InlineData(ArmazemDestino.Sucata, null, null)]
    public async Task Situacao_DepoisDeUmaSaida_PropoeAOrigemEOEstado(ArmazemDestino destino, ArmazemOrigem? origem, FerramentaEstadoTipo? estado)
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());
        await Servico(db).SaidaAsync(Saida(destino, reparador: destino == ArmazemDestino.Reparacao ? 1 : null));

        var situacao = (await Servico(db).SituacaoAsync(Cm))!;

        Assert.False(situacao.NoArmazem);
        Assert.Equal((destino, origem, estado), (situacao.UltimaSaida!.Value, situacao.OrigemProposta, situacao.EstadoProposto));
        Assert.Equal(destino == ArmazemDestino.Reparacao ? "Moldes Lda" : null, situacao.UltimaSaidaReparador);
    }

    [Fact]
    public async Task Situacao_SemMovimentos_EstaNoFabricoSemPropostas()
    {
        using var db = CreateContext();

        var situacao = (await Servico(db).SituacaoAsync(Cm))!;

        Assert.Equal((FerramentaLocalizacaoTipo.Fabrico, (string?)null, (ArmazemOrigem?)null, (FerramentaEstadoTipo?)null),
            (situacao.Localizacao, situacao.Posicao, situacao.OrigemProposta, situacao.EstadoProposto));
        Assert.Empty(situacao.UltimosMovimentos);
        // Todos os reparadores de CM e MF recebem tudo (Diogo, 27/10/2026).
        Assert.Equal(["Moldes Lda", "Só MF"], situacao.Reparadores.Select(r => r.Nome));
        Assert.Null(await Servico(db).SituacaoAsync(ToolRegistryConstants.RemovedToolId));
        Assert.Null(await Servico(db).SituacaoAsync("nao-existe"));
    }

    [Fact]
    public async Task Situacao_NoArmazem_MostraAPosicaoOEstadoEOsUltimosMovimentos()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, reparador: 1));
        await Servico(db).EntradaAsync(Entrada(posicao: "0043") with { Origem = ArmazemOrigem.Reparacao });

        var situacao = (await Servico(db).SituacaoAsync(Cm))!;

        Assert.Equal(("0043", FerramentaEstadoTipo.Reparado), (situacao.Posicao, situacao.Estado));
        Assert.Equal(
            ["Entrada 0043 · Reparação · Reparado", "Saída 0042 para Reparação · Moldes Lda", "Entrada 0042 · Reparado"],
            situacao.UltimosMovimentos.Select(m => m.Descricao));
    }

    [Fact]
    public async Task Corrigir_ParaOutraPosicao_LibertaAAnteriorEOcupaACerta()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var saida = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "0043", "engano"));

        Assert.Equal((ArmazemMovimentoResultado.Gravado, "0043"), (saida.Resultado, saida.Posicao));
        Assert.Equal(2, (await db.ArmazemOcupacoes.AsNoTracking().SingleAsync(o => o.LibertadaEmUtc == null)).PosicaoId);
        var movimento = await db.ArmazemMovimentos.AsNoTracking().SingleAsync(m => m.Tipo == ArmazemMovimentoTipo.Corrigir);
        Assert.Equal(((int?)2, (int?)1, "engano"), (movimento.PosicaoId, movimento.PosicaoAnteriorId, movimento.Observacoes));
    }

    [Fact]
    public async Task Corrigir_NaoEstaNoArmazemDepoisDaSucata_TiraDaPosicaoEVoltaAoFabrico()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());
        await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, null, null));
        await Servico(db).EntradaAsync(Entrada());
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Sucata, estado: FerramentaEstadoTipo.Sucatado));

        var saida = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "0042", null));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.Equal(FerramentaLocalizacaoTipo.Fabrico, (await db.FerramentaLocalizacoes.AsNoTracking().SingleAsync()).Localizacao);
        Assert.Equal(FerramentaEstadoTipo.Sucatado, (await db.FerramentaEstados.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync()).Estado);
        Assert.Equal(3, await db.ArmazemOcupacoes.CountAsync());
        Assert.Equal(1, (await db.ArmazemOcupacoes.AsNoTracking().SingleAsync(o => o.LibertadaEmUtc == null)).PosicaoId);
    }

    [Fact]
    public async Task Corrigir_ParaOndeJaEsta_NadaMuda()
    {
        using var db = CreateContext();
        var foraSemMudar = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, " ", null));
        await Servico(db).EntradaAsync(Entrada());

        var mesmaPosicao = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "0042", null));

        Assert.Equal(ArmazemMovimentoResultado.NadaAMudar, foraSemMudar.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.NadaAMudar, mesmaPosicao.Resultado);
        Assert.Equal(1, await db.ArmazemMovimentos.CountAsync());
    }

    [Fact]
    public async Task Corrigir_ParaUmaPosicaoQueNaoExiste_PedeParaCriar()
    {
        using var db = CreateContext();

        var semCriar = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "5001", null));
        var comCriar = await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "5001", null, CriarPosicao: true));

        Assert.Equal(ArmazemMovimentoResultado.PosicaoNaoExiste, semCriar.Resultado);
        Assert.Equal(ArmazemMovimentoResultado.Gravado, comCriar.Resultado);
        Assert.Equal(1, await db.ArmazemPosicoes.CountAsync(p => p.Codigo == "5001"));
    }

    [Fact]
    public async Task Saida_DepoisDeUmCorrigir_HerdaALinhaDaEntradaAnterior_BQ_B04()
    {
        using var db = CreateContext();
        var linha = new DMO.Alpha.Core.Production.Machine { Code = "B2" };
        db.Machines.Add(linha);
        await db.SaveChangesAsync();

        // Entrada com a Linha do formulário, na posição "0042".
        await Servico(db).EntradaAsync(Entrada() with { LinhaEReparadorDoFormulario = true, LinhaId = linha.Id });
        // Um Corrigir no meio, para OUTRA posição (não é uma saída real).
        await Servico(db).CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Cm, "0043", "engano"));

        // A Saída para Reparação (não Produção) herda a Linha/Job On do
        // último movimento REAL (a Entrada), saltando o Corrigir (BQ-B04):
        // antes da correção, o Corrigir era o «último movimento» e a Linha
        // perdia-se.
        var saida = await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, reparador: 1));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        var movimentoDeSaida = await db.ArmazemMovimentos.AsNoTracking()
            .SingleAsync(m => m.Tipo == ArmazemMovimentoTipo.Saida);
        Assert.Equal(linha.Id, movimentoDeSaida.MachineId);
    }

    [Theory]
    [InlineData("5447", new[] { Cm, Mf })]
    [InlineData("t17", new[] { Bq, Cm, Mf })]
    [InlineData("01", new[] { Bq })]
    [InlineData("0043", new[] { "cm-1075" })]
    [InlineData("REMOVED", new string[0])]
    [InlineData("  ", new string[0])]
    public async Task Procurar_PelaReferenciaLoteOuPosicao(string texto, string[] esperadas)
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: "cm-1075", posicao: "0043"));

        var encontradas = await Servico(db).ProcurarAsync(texto);

        Assert.Equal(esperadas.Order(), encontradas.Select(e => e.ToolId).Order());
    }

    [Fact]
    public async Task Procurar_DizOndeEstaCadaFerramenta()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));
        await Servico(db).EntradaAsync(Entrada(toolId: Mf));
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Reparacao, toolId: Mf, reparador: 2));

        var encontradas = (await Servico(db).ProcurarAsync("5447")).ToDictionary(e => e.ToolId);

        Assert.Equal(("Armazém · posição 0042", true), (encontradas[Cm].OndeEsta, encontradas[Cm].NoArmazem));
        Assert.Equal(("Reparação · Só MF", false), (encontradas[Mf].OndeEsta, encontradas[Mf].NoArmazem));
    }

    [Fact]
    public async Task Localizacao_EmprestadaDizAFabricaEscolhidaNaSaida()
    {
        // Diogo, 28/10/2026: o sítio concreto da Saída («Reparação · Moldin»).
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));
        await Servico(db).EntradaAsync(Entrada(toolId: Mf));
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Emprestado, toolId: Cm, fabrica: "Fábrica Norte"));
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Emprestado, toolId: Mf));

        var encontradas = (await Servico(db).ProcurarAsync("5447")).ToDictionary(e => e.ToolId);
        var situacao = (await Servico(db).SituacaoAsync(Cm))!;

        Assert.Equal("Emprestado · Fábrica Norte", encontradas[Cm].OndeEsta);
        Assert.Equal("Emprestado", encontradas[Mf].OndeEsta);
        Assert.Equal("Emprestado · Fábrica Norte", ArmazemTextos.Localizacao(situacao.Localizacao, situacao.LocalizacaoPormenor));
    }

    [Fact]
    public async Task Procurar_DaOEstadoAsMaquinasEOUltimoUso()
    {
        // Diogo, 21/10/2026: «posição, referência, lote, estado, máquinas, % uso».
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));
        db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = Cm, Machine = new DMO.Alpha.Core.Production.Machine { Code = "C3" } },
            new ToolMachineLink { ToolId = Cm, Machine = new DMO.Alpha.Core.Production.Machine { Code = "B1" } });
        db.FerramentaUtilizacoes.AddRange(
            new FerramentaUtilizacao { ToolId = Cm, Percentagem = 40m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-9) },
            new FerramentaUtilizacao { ToolId = Cm, Percentagem = 47.5m, RegistadaEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var encontradas = (await Servico(db).ProcurarAsync("5447")).ToDictionary(e => e.ToolId);

        // A CM: o estado da Entrada, as máquinas por ordem e a leitura mais recente.
        Assert.Equal(FerramentaEstadoTipo.Reparado, encontradas[Cm].Estado);
        Assert.Equal(["B1", "C3"], encontradas[Cm].Maquinas);
        Assert.Equal(47.5m, encontradas[Cm].Utilizacao);
        // O MF nunca entrou nem foi lido: sem estado, sem máquinas (todas) e sem uso.
        Assert.Null(encontradas[Mf].Estado);
        Assert.Empty(encontradas[Mf].Maquinas!);
        Assert.Null(encontradas[Mf].Utilizacao);
    }

    [Fact]
    public async Task Ocupantes_SoAsFerramentasQueAindaLaEstao()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada(toolId: Cm));
        await Servico(db).EntradaAsync(Entrada(toolId: Mf));
        await Servico(db).SaidaAsync(Saida(ArmazemDestino.Producao, toolId: Cm));

        var ocupantes = await Servico(db).OcupantesAsync([1, 2]);

        Assert.Equal([Mf], ocupantes[1].Select(o => o.ToolId));
        Assert.False(ocupantes.ContainsKey(2));
    }

    [Fact]
    public async Task EliminarAFerramenta_PassaOsMovimentosParaARemovedELibertaAPosicao()
    {
        using var db = CreateContext();
        await Servico(db).EntradaAsync(Entrada());

        var resultado = await new DeleteToolHandler(db).HandleAsync(new DeleteTool(Cm));

        Assert.Equal(DeleteToolResult.Success, resultado.Result);
        var movimento = await db.ArmazemMovimentos.AsNoTracking().SingleAsync();
        Assert.Equal((ToolRegistryConstants.RemovedToolId, "5447T173", "3"), (movimento.ToolId, movimento.ToolReferenceFrozen, movimento.ToolLotFrozen));
        var ocupacao = await db.ArmazemOcupacoes.AsNoTracking().SingleAsync();
        Assert.Equal(ToolRegistryConstants.RemovedToolId, ocupacao.ToolId);
        Assert.NotNull(ocupacao.LibertadaEmUtc);
        Assert.Empty(await db.FerramentaLocalizacoes.ToListAsync());
        Assert.Empty(await db.FerramentaEstados.ToListAsync());
        Assert.Empty(await Servico(db).OcupantesAsync([1]));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

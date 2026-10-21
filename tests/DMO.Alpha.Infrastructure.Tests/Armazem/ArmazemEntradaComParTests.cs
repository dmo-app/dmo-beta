using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// A Entrada da CM e do MF juntos (0.2.0-beta.11, como no Access): com o par
/// escolhido, a CM e o MF da mesma referência e lote entram na mesma posição
/// no mesmo envio, cada um com o seu movimento, ocupação, localização e
/// estado. Se o par já está no armazém, entra só a ferramenta e fica o aviso.
/// A situação mostra o par e onde está.
/// </summary>
public sealed class ArmazemEntradaComParTests
{
    private const string Cm = "cm-5447";
    private const string Mf = "mf-5447";

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-par-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "mf-5447-4", Type = ToolType.Mf, Reference = "5447T173", Lot = "4" },
            new Tool { ToolId = "cm-1075", Type = ToolType.Cm, Reference = "1075C142", Lot = "2" },
            new Tool { ToolId = "bq-t173", Type = ToolType.Bq, Reference = "5447T173", Lot = "3" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Id = 2, Codigo = "0043", CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemMovimentos Servico(DmoDbContext db) =>
        new(db, new OperacoesRegistadas(db), new Sessao());

    private static PedidoDeEntrada Entrada(string toolId = Cm, string posicao = "0042", bool comPar = true, Guid? operacao = null) =>
        new(operacao ?? Guid.NewGuid(), toolId, posicao, ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado, null, null, ComPar: comPar);

    [Theory]
    [InlineData(Cm, Mf)]
    [InlineData(Mf, Cm)]
    public async Task ComOPar_ACmEOMfEntramNaMesmaPosicao(string entra, string par)
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(entra));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.True(saida.ComPar);
        Assert.Empty(saida.Avisos);
        var movimentos = await db.ArmazemMovimentos.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal([entra, par], movimentos.Select(m => m.ToolId).ToArray());
        Assert.All(movimentos, m => Assert.Equal((ArmazemMovimentoTipo.Entrada, 1, ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado),
            (m.Tipo, m.PosicaoId!.Value, m.Origem!.Value, m.Estado!.Value)));
        Assert.Equal(2, movimentos.Select(m => m.OperacaoId).Distinct().Count());
        Assert.Equal(1, await db.OperacoesRegistadas.CountAsync());
        var ocupacoes = await db.ArmazemOcupacoes.AsNoTracking().ToListAsync();
        Assert.Equal([(entra, 1), (par, 1)], ocupacoes.OrderBy(o => o.MovimentoId).Select(o => (o.ToolId, o.PosicaoId)).ToArray());
        Assert.All(ocupacoes, o => Assert.Contains(movimentos, m => m.Id == o.MovimentoId && m.ToolId == o.ToolId));
        Assert.Equal(2, await db.FerramentaLocalizacoes.CountAsync(l => l.Localizacao == FerramentaLocalizacaoTipo.Fabrico));
        Assert.Equal(2, await db.FerramentaEstados.CountAsync(e => e.Estado == FerramentaEstadoTipo.Reparado));
    }

    [Fact]
    public async Task SemOPar_EntraSoAFerramenta()
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(comPar: false));

        Assert.False(saida.ComPar);
        Assert.Equal([Cm], await db.ArmazemMovimentos.Select(m => m.ToolId).ToArrayAsync());
    }

    [Fact]
    public async Task OParJaNoArmazem_EntraSoAFerramentaEAvisa()
    {
        using var db = CreateContext();
        var servico = Servico(db);
        await servico.EntradaAsync(Entrada(Mf, "0043", comPar: false));

        var saida = await servico.EntradaAsync(Entrada(Cm));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.False(saida.ComPar);
        Assert.Equal(["MF 5447T173 · lote 3 já está no armazém, na posição 0043: entrou só CM."], saida.Avisos);
        Assert.Equal(2, await db.ArmazemMovimentos.CountAsync());
        Assert.Equal(1, await db.ArmazemOcupacoes.CountAsync(o => o.ToolId == Mf && o.PosicaoId == 2));
    }

    [Theory]
    [InlineData("bq-t173")]
    [InlineData("cm-1075")]
    public async Task SemParQueExista_EntraSoAFerramenta(string toolId)
    {
        using var db = CreateContext();

        var saida = await Servico(db).EntradaAsync(Entrada(toolId));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        Assert.False(saida.ComPar);
        Assert.Empty(saida.Avisos);
        Assert.Equal([toolId], await db.ArmazemMovimentos.Select(m => m.ToolId).ToArrayAsync());
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_GravaOParUmaVez()
    {
        using var db = CreateContext();
        var servico = Servico(db);
        var operacao = Guid.NewGuid();

        await servico.EntradaAsync(Entrada(operacao: operacao));
        db.ChangeTracker.Clear();
        var segunda = await servico.EntradaAsync(Entrada(operacao: operacao));

        Assert.Equal(ArmazemMovimentoResultado.JaRegistado, segunda.Resultado);
        Assert.Equal(2, await db.ArmazemMovimentos.CountAsync());
        Assert.Equal(2, await db.ArmazemOcupacoes.CountAsync());
    }

    [Fact]
    public async Task ASituacao_MostraOParEOndeEsta()
    {
        using var db = CreateContext();
        var servico = Servico(db);

        Assert.Equal(new ArmazemPar(Mf, ToolType.Mf, null), (await servico.SituacaoAsync(Cm))!.Par);
        await servico.EntradaAsync(Entrada(Mf, "0043", comPar: false));
        Assert.Equal(new ArmazemPar(Mf, ToolType.Mf, "0043"), (await servico.SituacaoAsync(Cm))!.Par);
        Assert.Equal(new ArmazemPar(Cm, ToolType.Cm, null), (await servico.SituacaoAsync(Mf))!.Par);
        // Outro lote, a BQ e o registo eliminado não são o par.
        Assert.Null((await servico.SituacaoAsync("mf-5447-4"))!.Par);
        Assert.Null((await servico.SituacaoAsync("bq-t173"))!.Par);
        Assert.Null((await servico.SituacaoAsync("cm-1075"))!.Par);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

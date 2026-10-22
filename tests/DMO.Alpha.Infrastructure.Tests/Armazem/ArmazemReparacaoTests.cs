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
/// A Reparação do Armazém (Diogo, 22/10/2026): «Fora, em reparação» é cada
/// CM ou MF cujo último movimento é uma Saída para Reparação (as BQ seguem-se
/// nas Boquilhas); as últimas entradas e saídas (o Histórico,
/// agora na função Movimentos) têm as BQ, o reparador (também na Entrada
/// que volta da Reparação), a posição e os filtros novos.
/// </summary>
public sealed class ArmazemReparacaoTests
{
    private const string Cm = "cm-5447";
    private const string Mf = "mf-6012";
    private const string Cm2 = "cm-7001";
    private const string Bq = "bq-t173";

    private static DateTime Dia(int dia) => new(2026, 10, dia, 9, 0, 0, DateTimeKind.Utc);

    private static async Task<DmoDbContext> CreateContextAsync()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-reparacao-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "6012A118", Lot = "7" },
            new Tool { ToolId = Cm2, Type = ToolType.Cm, Reference = "7001B200", Lot = "1" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = Dia(1) },
            new ArmazemPosicao { Id = 2, Codigo = "0043", CriadaEmUtc = Dia(1) },
            new ArmazemPosicao { Id = 3, Codigo = "0044", CriadaEmUtc = Dia(1) },
            new ArmazemPosicao { Id = 4, Codigo = "5001", CriadaEmUtc = Dia(1) });
        db.Reparadores.AddRange(new Reparador { Id = 1, Nome = "Moldes Lda" }, new Reparador { Id = 2, Nome = "Afia Norte" });
        db.BqRepairers.Add(new BqRepairer { Id = 7, Name = "LE" });
        db.SaveChanges();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, Dia(10), null));
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Mf, "0043", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, Dia(10), null));
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm2, "0044", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, Dia(10), null));
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Bq, "5001", ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, Dia(10), null));
        // A CM vai à reparação e volta para a 0042.
        await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm, ArmazemDestino.Reparacao, 1, null, null, Dia(11), null));
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado, Dia(14), null));
        // O MF e a BQ vão à reparação e ficam lá; a outra CM vai para a Produção.
        await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Mf, ArmazemDestino.Reparacao, 2, null, null, Dia(12), null));
        await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Bq, ArmazemDestino.Reparacao, 7, null, null, Dia(13), null));
        await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm2, ArmazemDestino.Producao, null, null, null, Dia(13), null));
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task Fora_SoCmEMfCujoUltimoMovimentoESaidaParaReparacao()
    {
        using var db = await CreateContextAsync();

        var fora = await new ArmazemReparacao(db).ForaAsync();

        var mf = Assert.Single(fora);
        Assert.Equal((Mf, ToolType.Mf, "6012A118", "7", "Afia Norte", Dia(12)),
            (mf.ToolId, mf.Tipo, mf.Referencia, mf.Lote, mf.Reparador, mf.SaiuEmUtc));
        Assert.DoesNotContain(fora, f => f.ToolId == Bq);
    }

    [Fact]
    public async Task Ultimas_BqIncluida_ComReparadorEPosicao()
    {
        using var db = await CreateContextAsync();

        var lista = await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(De: new DateOnly(2026, 10, 11)), 1);

        Assert.Equal(
            [
                (Cm, ArmazemMovimentoTipo.Entrada, (string?)"Moldes Lda", (string?)"0042"),
                (Cm2, ArmazemMovimentoTipo.Saida, null, null),
                (Bq, ArmazemMovimentoTipo.Saida, "LE", null),
                (Mf, ArmazemMovimentoTipo.Saida, "Afia Norte", null),
                (Cm, ArmazemMovimentoTipo.Saida, "Moldes Lda", null)
            ],
            lista.Items.Select(m => (m.ToolId!, m.Movimento, m.Reparador, m.Posicao)).ToArray());
    }

    [Fact]
    public async Task Ultimas_OsFiltrosNovos()
    {
        using var db = await CreateContextAsync();
        db.Machines.Add(new DMO.Alpha.Core.Production.Machine { Id = 1, Code = "L07" });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = Mf, MachineId = 1 });
        db.SaveChanges();
        var h = new ArmazemHistorico(db);
        async Task<string[]> Ids(ArmazemHistoricoFiltro f) => (await h.ListarAsync(f, 1)).Items.Select(m => m.ToolId!).Distinct().ToArray();

        Assert.Equal([Bq], await Ids(new ArmazemHistoricoFiltro(TipoFerramenta: ToolType.Bq)));
        Assert.Equal([Cm], await Ids(new ArmazemHistoricoFiltro(Referencia: "5447")));
        Assert.Equal([Mf], await Ids(new ArmazemHistoricoFiltro(Lote: "7")));
        Assert.Equal([Mf], await Ids(new ArmazemHistoricoFiltro(Linha: "L07")));
        // O reparador: a Saída para ele e a Entrada que volta dele.
        var moldes = (await h.ListarAsync(new ArmazemHistoricoFiltro(Reparador: "Moldes Lda"), 1)).Items;
        Assert.Equal([ArmazemMovimentoTipo.Entrada, ArmazemMovimentoTipo.Saida], moldes.Select(m => m.Movimento).ToArray());
        Assert.All(moldes, m => Assert.Equal(Cm, m.ToolId));
        Assert.Equal(["Afia Norte", "LE", "Moldes Lda"], await h.ReparadoresAsync());
        Assert.Equal(["L07"], await h.LinhasAsync());
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

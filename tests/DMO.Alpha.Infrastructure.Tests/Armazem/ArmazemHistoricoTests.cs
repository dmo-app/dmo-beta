using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// O Histórico do Armazém (0.2.0-beta.12): os movimentos do mais recente para
/// o mais antigo, filtrados pela referência, lote ou posição (a de entrada e
/// a de onde saiu), pela ferramenta, pelo movimento, pelo destino e pelos dias
/// (hora de Lisboa, os dois dias inclusive). Uma ferramenta eliminada fica
/// com a referência e o lote guardados.
/// </summary>
public sealed class ArmazemHistoricoTests
{
    private const string Cm = "cm-5447";
    private const string Mf = "mf-6012";

    // 10/10/2026 a 13/10/2026 às 23:30 de Lisboa (22:30 UTC, hora de verão).
    private static DateTime Dia(int dia) => new(2026, 10, dia, 22, 30, 0, DateTimeKind.Utc);

    private static async Task<DmoDbContext> CreateContextAsync()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-historico-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "6012A118", Lot = "7" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = Dia(1) },
            new ArmazemPosicao { Id = 2, Codigo = "0043", CriadaEmUtc = Dia(1) });
        db.Reparadores.Add(new Reparador { Id = 1, Nome = "Moldes Lda" });
        db.SaveChanges();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, Dia(10), "chegou"));
        await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm, ArmazemDestino.Reparacao, 1, null, null, Dia(11), null));
        await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Mf, "0043", ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, Dia(12), null));
        await s.CorrigirAsync(new PedidoDeCorrecao(Guid.NewGuid(), Mf, "0042", null));
        db.ChangeTracker.Clear();
        // O Corrigir grava agora; põe-no no dia 13 para os filtros dos dias.
        var corrigir = db.ArmazemMovimentos.Single(m => m.Tipo == ArmazemMovimentoTipo.Corrigir);
        corrigir.OcorridoEmUtc = Dia(13);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<string[]> Descricoes(DmoDbContext db, ArmazemHistoricoFiltro filtro) =>
        (await new ArmazemHistorico(db).ListarAsync(filtro, 1)).Items.Select(l => l.Descricao).ToArray();

    [Fact]
    public async Task SemFiltro_DoMaisRecenteParaOMaisAntigo()
    {
        using var db = await CreateContextAsync();

        var lista = await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(), 1);

        Assert.Equal(4, lista.TotalCount);
        Assert.Equal(
            [
                (Mf, "Corrigida para a posição 0042"),
                (Mf, "Entrada na posição 0043 · Nova · Novo"),
                (Cm, "Saída da posição 0042 para Reparação · Moldes Lda"),
                (Cm, "Entrada na posição 0042 · Já estava no armazém · Reparado")
            ],
            lista.Items.Select(l => (l.ToolId!, l.Descricao)).ToArray());
        var primeira = lista.Items[^1];
        Assert.Equal((ToolType.Cm, "5447T173", "3", "chegou", "Rui"), (primeira.Tipo!.Value, primeira.Referencia, primeira.Lote, primeira.Observacoes!, primeira.RegistadoPor!));
    }

    [Theory]
    [InlineData("5447", 2)]
    [InlineData("t173", 2)]
    [InlineData("7", 2)]
    [InlineData("0043", 2)]
    [InlineData("0042", 3)]
    [InlineData("9999", 0)]
    public async Task OTexto_ProcuraAReferenciaOLoteOuAPosicao(string texto, int quantos)
    {
        using var db = await CreateContextAsync();

        Assert.Equal(quantos, (await Descricoes(db, new ArmazemHistoricoFiltro(Texto: texto))).Length);
    }

    [Fact]
    public async Task AFerramentaOMovimentoEODestino()
    {
        using var db = await CreateContextAsync();

        Assert.Equal(2, (await Descricoes(db, new ArmazemHistoricoFiltro(ToolId: Mf))).Length);
        Assert.Equal(
            ["Entrada na posição 0043 · Nova · Novo", "Entrada na posição 0042 · Já estava no armazém · Reparado"],
            await Descricoes(db, new ArmazemHistoricoFiltro(Tipo: ArmazemMovimentoTipo.Entrada)));
        Assert.Single(await Descricoes(db, new ArmazemHistoricoFiltro(Destino: ArmazemDestino.Reparacao)));
        Assert.Empty(await Descricoes(db, new ArmazemHistoricoFiltro(Destino: ArmazemDestino.Sucata)));
    }

    [Theory]
    [InlineData(11, null, 3)]
    [InlineData(null, 11, 2)]
    [InlineData(12, 12, 1)]
    [InlineData(14, null, 0)]
    public async Task OsDias_EmHoraDeLisboa_InclusiveNasPontas(int? de, int? ate, int quantos)
    {
        using var db = await CreateContextAsync();

        // 22:30 UTC é 23:30 em Lisboa: ainda é o próprio dia.
        var filtro = new ArmazemHistoricoFiltro(
            De: de is { } d ? new DateOnly(2026, 10, d) : null,
            Ate: ate is { } a ? new DateOnly(2026, 10, a) : null);
        Assert.Equal(quantos, (await Descricoes(db, filtro)).Length);
    }

    [Fact]
    public async Task MeiaNoiteEMeiaEmLisboa_JaContaComoODiaSeguinte()
    {
        using var db = await CreateContextAsync();
        // 23:30 UTC de 13/10 é 00:30 de 14/10 em Lisboa (hora de verão).
        var corrigir = db.ArmazemMovimentos.Single(m => m.Tipo == ArmazemMovimentoTipo.Corrigir);
        corrigir.OcorridoEmUtc = new DateTime(2026, 10, 13, 23, 30, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var dia13 = new DateOnly(2026, 10, 13);
        var dia14 = new DateOnly(2026, 10, 14);
        Assert.DoesNotContain("Corrigida para a posição 0042", await Descricoes(db, new ArmazemHistoricoFiltro(De: dia13, Ate: dia13)));
        Assert.Equal(["Corrigida para a posição 0042"], await Descricoes(db, new ArmazemHistoricoFiltro(De: dia14, Ate: dia14)));
    }

    [Fact]
    public async Task UmaFerramentaEliminada_FicaComAReferenciaGuardada()
    {
        using var db = await CreateContextAsync();
        // Como o eliminar faz: o movimento passa para o registo REMOVED com a referência e o lote guardados.
        foreach (var m in db.ArmazemMovimentos.Where(m => m.ToolId == Cm))
        {
            (m.ToolId, m.ToolReferenceFrozen, m.ToolLotFrozen) = (ToolRegistryConstants.RemovedToolId, "5447T173", "3");
        }

        await db.SaveChangesAsync();

        var lista = await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(Texto: "5447"), 1);

        Assert.Equal(2, lista.Items.Count);
        Assert.All(lista.Items, l => Assert.Equal((true, (ToolType?)null, "5447T173", "3"), (l.Eliminada, l.Tipo, l.Referencia, l.Lote)));
        Assert.Equal(2, (await Descricoes(db, new ArmazemHistoricoFiltro(Texto: "3"))).Length);
    }

    [Fact]
    public async Task APaginacao_VinteECincoPorPagina()
    {
        using var db = await CreateContextAsync();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        for (var i = 0; i < 12; i++)
        {
            await s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Mf, ArmazemDestino.Producao, null, null, null, Dia(14).AddHours(i), null));
            await s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Mf, "0042", ArmazemOrigem.Producao, FerramentaEstadoTipo.PorReparar, Dia(14).AddHours(i).AddMinutes(30), null));
        }

        var segunda = await new ArmazemHistorico(db).ListarAsync(new ArmazemHistoricoFiltro(), 2);

        Assert.Equal((28, 2, 2), (segunda.TotalCount, segunda.PageNumber, segunda.TotalPages));
        Assert.Equal(3, segunda.Items.Count);
        Assert.Equal("Entrada na posição 0042 · Já estava no armazém · Reparado", segunda.Items[^1].Descricao);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

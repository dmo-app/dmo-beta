using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// O mapa do Armazém (0.2.0-beta.10): as posições agrupam-se por estante (os
/// três primeiros dos quatro algarismos: corredor e estante) e por corredor, cada armário diz quantas estão ocupadas e a
/// percentagem, a CM e o MF da mesma referência e lote contam como par, e o
/// filtro mostra as livres, as ocupadas ou as que têm uma ferramenta por
/// reparar (pelo último estado). Uma posição arquivada só conta enquanto tiver
/// ferramentas.
/// </summary>
public sealed class ArmazemMapaTests
{
    private static readonly DateTime Ontem = DateTime.UtcNow.AddDays(-1);

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-mapa-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = "cm-5447", Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "mf-5447", Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "cm-1075", Type = ToolType.Cm, Reference = "1075C142", Lot = "2" },
            new Tool { ToolId = "mf-1075", Type = ToolType.Mf, Reference = "1075C142", Lot = "9" },
            new Tool { ToolId = "bq-t173", Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "4001", CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 2, Codigo = "4002", CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 3, Codigo = "4003", CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 4, Codigo = "4004", Ativa = false, CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 5, Codigo = "4005", Ativa = false, CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 6, Codigo = "7001", CriadaEmUtc = Ontem },
            new ArmazemPosicao { Id = 7, Codigo = "5001", CriadaEmUtc = Ontem });
        db.ArmazemOcupacoes.AddRange(
            Ocupacao(1, "cm-5447", 1),
            Ocupacao(2, "mf-5447", 1),
            Ocupacao(3, "cm-1075", 2),
            Ocupacao(4, "mf-1075", 2),
            Ocupacao(5, "bq-t173", 5),
            // Uma ocupação já libertada não conta.
            new ArmazemOcupacao { Id = 6, ToolId = "bq-t173", PosicaoId = 3, MovimentoId = 1, DesdeUtc = Ontem.AddDays(-5), LibertadaEmUtc = Ontem });
        db.FerramentaEstados.AddRange(
            new FerramentaEstado { Id = 1, ToolId = "cm-1075", Estado = FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = Ontem },
            new FerramentaEstado { Id = 2, ToolId = "cm-5447", Estado = FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = Ontem },
            new FerramentaEstado { Id = 3, ToolId = "cm-5447", Estado = FerramentaEstadoTipo.Reparado, RegistadoEmUtc = Ontem });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemOcupacao Ocupacao(int id, string toolId, int posicaoId) =>
        new() { Id = id, ToolId = toolId, PosicaoId = posicaoId, MovimentoId = id, DesdeUtc = Ontem };

    [Theory]
    [InlineData("1033", "103", "1", "3", "Corredor 1 · Estante 03")]
    [InlineData("4048", "404", "4", "8", "Corredor 4 · Estante 04")]
    [InlineData("0042", "004", "0", "2", "Corredor 0 · Estante 04")]
    public void APosicao_CorredorEstanteEPrateleira(string codigo, string estante, string corredor, string prateleira, string nome)
    {
        // Diogo 21/10 (AR-03): 1.º corredor, 2.º e 3.º estante, 4.º prateleira.
        Assert.Equal(estante, ArmazemArmarios.De(codigo));
        Assert.Equal(corredor, ArmazemArmarios.Corredor(codigo));
        Assert.Equal(prateleira, ArmazemArmarios.Prateleira(codigo));
        Assert.Equal(nome, ArmazemArmarios.Nome(estante));
        Assert.Equal("Corredor " + corredor, ArmazemArmarios.Nome(corredor));
    }

    [Fact]
    public async Task SemArmario_AbreOPrimeiro_ComAOcupacao()
    {
        using var db = CreateContext();

        var mapa = await new ArmazemMapa(db).MapaAsync(null, ArmazemMapaFiltro.Todas);

        // A posição 4004 está arquivada e vazia: não conta; a 4005 está arquivada com a BQ: conta.
        Assert.Equal(
            [("400", 4, 3, 75), ("500", 1, 0, 0), ("700", 1, 0, 0)],
            mapa.Armarios.Select(a => (a.Chave, a.Posicoes, a.Ocupadas, a.Percentagem)).ToArray());
        Assert.Equal("400", mapa.Armario);
        Assert.Equal((4, 3, 75), (mapa.Posicoes, mapa.Ocupadas, mapa.Percentagem));
        Assert.Equal(["4001", "4002", "4003", "4005"], mapa.Lista.Select(p => p.Codigo).ToArray());
    }

    [Fact]
    public async Task OParDaMesmaReferenciaELote_NaoEMisturado()
    {
        using var db = CreateContext();

        var lista = (await new ArmazemMapa(db).MapaAsync("400", ArmazemMapaFiltro.Todas)).Lista;

        var par = lista.Single(p => p.Codigo == "4001");
        Assert.True(par.Par);
        Assert.False(par.Misturada);
        Assert.Equal([ToolType.Cm, ToolType.Mf], par.Ferramentas.Select(f => f.Tipo).ToArray());
        // A mesma referência com lotes diferentes já é uma mistura.
        var mistura = lista.Single(p => p.Codigo == "4002");
        Assert.False(mistura.Par);
        Assert.True(mistura.Misturada);
        Assert.True(lista.Single(p => p.Codigo == "4003").Livre);
        Assert.False(lista.Single(p => p.Codigo == "4005").Ativa);
    }

    [Theory]
    [InlineData(ArmazemMapaFiltro.Livres, new[] { "4003" })]
    [InlineData(ArmazemMapaFiltro.Ocupadas, new[] { "4001", "4002", "4005" })]
    [InlineData(ArmazemMapaFiltro.PorReparar, new[] { "4002" })]
    public async Task OFiltro_ContaSempreOArmarioInteiro(ArmazemMapaFiltro filtro, string[] codigos)
    {
        using var db = CreateContext();

        var mapa = await new ArmazemMapa(db).MapaAsync("400", filtro);

        // A CM 5447 esteve por reparar mas o último estado é Reparado.
        Assert.Equal(codigos, mapa.Lista.Select(p => p.Codigo).ToArray());
        Assert.Equal((4, 3), (mapa.Posicoes, mapa.Ocupadas));
    }

    [Theory]
    [InlineData("*", "*", 6, 3)]
    [InlineData("500", "500", 1, 0)]
    [InlineData("4", "4", 4, 3)]
    [InlineData("99", "400", 4, 3)]
    [InlineData("9", "400", 4, 3)]
    public async Task TodosOsArmarios_OuUmQueNaoExisteAbreOPrimeiro(string pedido, string aberto, int posicoes, int ocupadas)
    {
        using var db = CreateContext();

        var mapa = await new ArmazemMapa(db).MapaAsync(pedido, ArmazemMapaFiltro.Todas);

        Assert.Equal(aberto, mapa.Armario);
        Assert.Equal((posicoes, ocupadas), (mapa.Posicoes, mapa.Ocupadas));
        Assert.Equal(posicoes, mapa.Lista.Count);
    }

    [Fact]
    public async Task SemPosicoes_UmMapaVazio()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-mapa-vazio-" + Guid.NewGuid().ToString("N"))
            .Options);

        var mapa = await new ArmazemMapa(db).MapaAsync(null, ArmazemMapaFiltro.Todas);

        Assert.Empty(mapa.Armarios);
        Assert.Null(mapa.Armario);
        Assert.Equal((0, 0, 0), (mapa.Posicoes, mapa.Ocupadas, mapa.Percentagem));
        Assert.Empty(mapa.Lista);
    }
}

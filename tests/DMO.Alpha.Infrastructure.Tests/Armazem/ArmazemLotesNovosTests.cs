using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// Os lotes novos do Armazém (Diogo, 22/10/2026): o Armazém guarda a
/// referência, o lote e a posição como «por criar» sem criar a ferramenta;
/// ao resolver com a ferramenta criada (ou uma existente), a entrada fica na
/// posição guardada, à data do registo, e o registo sai dos por criar.
/// </summary>
public sealed class ArmazemLotesNovosTests
{
    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-lotes-novos-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.Add(new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemLotesNovos Servico(DmoDbContext db)
    {
        var sessao = new Sessao();
        return new(db, new ArmazemMovimentos(db, new OperacoesRegistadas(db), sessao), sessao);
    }

    [Theory]
    [InlineData(null, "13", "1033", ArmazemLoteNovoResultado.SemReferencia)]
    [InlineData("5447", " ", "1033", ArmazemLoteNovoResultado.SemLote)]
    [InlineData("5447", "13", "103", ArmazemLoteNovoResultado.PosicaoInvalida)]
    [InlineData("5447", "13", "10A3", ArmazemLoteNovoResultado.PosicaoInvalida)]
    public async Task Registar_SoRecusaOQueFaltaEAPosicaoSemQuatroAlgarismos(string? referencia, string? lote, string posicao, ArmazemLoteNovoResultado esperado)
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(ToolType.Cm, referencia, lote, posicao);

        Assert.Equal(esperado, saida.Resultado);
        Assert.Empty(await db.ArmazemLotesNovos.ToListAsync());
    }

    [Fact]
    public async Task Registar_GuardaPorCriar_SemCriarFerramentaNemMovimento()
    {
        using var db = CreateContext();

        var saida = await Servico(db).RegistarAsync(ToolType.Cm, " 5447 ", " 13 ", "1033");

        Assert.True(saida.Gravou);
        var registo = Assert.Single(await db.ArmazemLotesNovos.ToListAsync());
        Assert.Equal(("5447", "13", "1033", ArmazemLoteNovoEstado.PorCriar, "Rui"), (registo.Referencia, registo.Lote, registo.Posicao, registo.Estado, registo.RegistadoPor));
        Assert.Equal(1, await db.Tools.CountAsync());
        Assert.Empty(await db.ArmazemMovimentos.ToListAsync());
        // A posição 1033 ainda não existe: grava e avisa.
        Assert.Contains(saida.Avisos!, a => a.Contains("1033", StringComparison.Ordinal));
        var porCriar = Assert.Single(await Servico(db).PorCriarAsync(ToolType.Cm));
        Assert.Equal(registo.Id, porCriar.Id);
        Assert.Empty(await Servico(db).PorCriarAsync(ToolType.Mf));
    }

    [Fact]
    public async Task Resolver_DaAEntradaNaPosicaoGuardada_EOUltimoMovimentoPoeLa()
    {
        using var db = CreateContext();
        var servico = Servico(db);
        var id = (await servico.RegistarAsync(ToolType.Cm, "5447", "13", "1033")).Id!.Value;
        var registadoEm = (await db.ArmazemLotesNovos.AsNoTracking().SingleAsync()).RegistadoEmUtc;
        db.Tools.Add(new Tool { ToolId = "cm-5447-13", Type = ToolType.Cm, Reference = "5447", Lot = "13" });
        await db.SaveChangesAsync();

        var resolucao = await servico.ResolverAsync(id, "cm-5447-13");
        var outraVez = await servico.ResolverAsync(id, "cm-5447-13");

        Assert.True(resolucao.Resolveu);
        Assert.Equal(ArmazemLoteNovoResolucao.JaResolvido, outraVez.Resultado);
        var movimento = Assert.Single(await db.ArmazemMovimentos.AsNoTracking().ToListAsync());
        Assert.Equal((ArmazemMovimentoTipo.Entrada, "cm-5447-13", ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo), (movimento.Tipo, movimento.ToolId, movimento.Origem, movimento.Estado));
        Assert.Equal(registadoEm, movimento.OcorridoEmUtc);
        var situacao = await new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao()).SituacaoAsync("cm-5447-13");
        Assert.Equal("1033", situacao!.Posicao);
        var registo = await db.ArmazemLotesNovos.AsNoTracking().SingleAsync();
        Assert.Equal((ArmazemLoteNovoEstado.Resolvido, "cm-5447-13", movimento.Id), (registo.Estado, registo.ResolvedToolId, registo.MovimentoId));
        Assert.Empty(await servico.PorCriarAsync());
    }

    [Fact]
    public async Task FerramentaExistente_ComAMesmaReferenciaELote_LigaEmVezDeCriar()
    {
        using var db = CreateContext();
        var servico = Servico(db);
        var id = (await servico.RegistarAsync(ToolType.Mf, "5447", "13", "0042")).Id!.Value;
        Assert.Null(await servico.FerramentaExistenteAsync(id));
        db.Tools.AddRange(
            new Tool { ToolId = "cm-5447-13", Type = ToolType.Cm, Reference = "5447", Lot = "13" },
            new Tool { ToolId = "mf-5447-13", Type = ToolType.Mf, Reference = "5447", Lot = "13" });
        await db.SaveChangesAsync();

        var existente = await servico.FerramentaExistenteAsync(id);
        var resolucao = await servico.ResolverAsync(id, existente!);

        Assert.Equal("mf-5447-13", existente);
        Assert.True(resolucao.Resolveu);
        Assert.Equal("mf-5447-13", (await db.ArmazemOcupacoes.AsNoTracking().SingleAsync()).ToolId);
    }

    [Fact]
    public async Task Resolver_ComFerramentaQueNaoExiste_NaoResolve()
    {
        using var db = CreateContext();
        var servico = Servico(db);
        var id = (await servico.RegistarAsync(ToolType.Bq, "T173", "01", "0042")).Id!.Value;

        var resolucao = await servico.ResolverAsync(id, "nao-existe");

        Assert.Equal(ArmazemLoteNovoResolucao.FerramentaNaoExiste, resolucao.Resultado);
        Assert.Single(await servico.PorCriarAsync());
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

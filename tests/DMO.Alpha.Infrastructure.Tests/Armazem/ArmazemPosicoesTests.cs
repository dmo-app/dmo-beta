using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// As posições do Armazém (0.2.0-beta.8): o código aceita as três formas das
/// bases Access (1001, 7001a, A001) e guarda-se em maiúsculas; o mesmo código
/// outra vez é «já existe»; arquivar e reativar não apagam nada; a lista
/// procura pelo início do código, filtra pelo estado e pagina.
/// </summary>
public sealed class ArmazemPosicoesTests
{
    private static DmoDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-posicoes-" + Guid.NewGuid().ToString("N"))
            .Options);

    [Theory]
    [InlineData("0042", "0042")]
    [InlineData(" 7001a ", "7001A")]
    [InlineData("A001", "A001")]
    [InlineData("e000", "E000")]
    public async Task Criar_AsTresFormasDoAccess_GuardaEmMaiusculas(string escrito, string guardado)
    {
        using var db = CreateContext();
        var posicoes = new ArmazemPosicoes(db, new Sessao());

        var (resultado, codigo) = await posicoes.CriarAsync(escrito);

        Assert.Equal((ArmazemPosicaoResultado.Gravado, guardado), (resultado, codigo));
        var posicao = await db.ArmazemPosicoes.AsNoTracking().SingleAsync();
        Assert.Equal(guardado, posicao.Codigo);
        Assert.True(posicao.Ativa);
        Assert.Equal("Rui", posicao.CriadaPor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00 42")]
    [InlineData("00-42")]
    [InlineData("12345678901234567")]
    [InlineData("posição")]
    public async Task Criar_ForaDaRegra_NaoGrava(string? escrito)
    {
        using var db = CreateContext();

        var (resultado, _) = await new ArmazemPosicoes(db, new Sessao()).CriarAsync(escrito);

        Assert.Equal(ArmazemPosicaoResultado.CodigoInvalido, resultado);
        Assert.Empty(await db.ArmazemPosicoes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Criar_OMesmoCodigoComOutrasMaiusculas_JaExiste()
    {
        using var db = CreateContext();
        var posicoes = new ArmazemPosicoes(db, new Sessao());
        await posicoes.CriarAsync("7001a");

        var (resultado, codigo) = await posicoes.CriarAsync("7001A");

        Assert.Equal((ArmazemPosicaoResultado.JaExiste, "7001A"), (resultado, codigo));
        Assert.Single(await db.ArmazemPosicoes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task MudarAtiva_ArquivaEReativa_SemApagar()
    {
        using var db = CreateContext();
        var posicoes = new ArmazemPosicoes(db, new Sessao());
        await posicoes.CriarAsync("0042");
        var id = (await db.ArmazemPosicoes.AsNoTracking().SingleAsync()).Id;

        Assert.Equal(ArmazemPosicaoResultado.Gravado, await posicoes.MudarAtivaAsync(id, ativa: false));
        Assert.False((await db.ArmazemPosicoes.AsNoTracking().SingleAsync()).Ativa);
        // O mesmo pedido outra vez (um duplo clique) não muda nada.
        Assert.Equal(ArmazemPosicaoResultado.Gravado, await posicoes.MudarAtivaAsync(id, ativa: false));
        Assert.Equal(ArmazemPosicaoResultado.Gravado, await posicoes.MudarAtivaAsync(id, ativa: true));
        Assert.True((await db.ArmazemPosicoes.AsNoTracking().SingleAsync()).Ativa);
        Assert.Equal(ArmazemPosicaoResultado.NaoExiste, await posicoes.MudarAtivaAsync(id + 99, ativa: false));
    }

    [Fact]
    public async Task Listar_PeloInicioDoCodigoEPeloEstado_PelaOrdemDoCodigo()
    {
        using var db = CreateContext();
        var posicoes = new ArmazemPosicoes(db, new Sessao());
        foreach (var codigo in new[] { "7002", "7001A", "1001", "A001", "7001" })
        {
            await posicoes.CriarAsync(codigo);
        }

        var arquivar = await db.ArmazemPosicoes.AsNoTracking().SingleAsync(p => p.Codigo == "7002");
        await posicoes.MudarAtivaAsync(arquivar.Id, ativa: false);

        var sete = await posicoes.ListarAsync("7", ArmazemPosicaoFiltro.Todas, 1);
        Assert.Equal(["7001", "7001A", "7002"], sete.Items.Select(p => p.Codigo));
        var seteAtivas = await posicoes.ListarAsync("7", ArmazemPosicaoFiltro.Ativas, 1);
        Assert.Equal(["7001", "7001A"], seteAtivas.Items.Select(p => p.Codigo));
        var arquivadas = await posicoes.ListarAsync(null, ArmazemPosicaoFiltro.Arquivadas, 1);
        Assert.Equal(["7002"], arquivadas.Items.Select(p => p.Codigo));
        // A procura não distingue maiúsculas: «a0» encontra «A001».
        Assert.Equal(["A001"], (await posicoes.ListarAsync("a0", ArmazemPosicaoFiltro.Todas, 1)).Items.Select(p => p.Codigo));
    }

    [Fact]
    public async Task Listar_MaisDoQueUmaPagina_Pagina()
    {
        using var db = CreateContext();
        var posicoes = new ArmazemPosicoes(db, new Sessao());
        for (var n = 1; n <= 30; n++)
        {
            await posicoes.CriarAsync($"{n:0000}");
        }

        var segunda = await posicoes.ListarAsync(null, ArmazemPosicaoFiltro.Todas, 2);

        Assert.Equal((30, 2, 2), (segunda.TotalCount, segunda.PageNumber, segunda.TotalPages));
        Assert.Equal(["0026", "0027", "0028", "0029", "0030"], segunda.Items.Select(p => p.Codigo));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A codificação por linha (Diogo, 28/10/2026): a tabela da folha da MG com
/// os nomes técnicos editáveis, o modo da B2 (Dupla/Tripla), o nome técnico e
/// as quantidades de uma ferramenta nova e o «Atualizar» das registadas.
/// </summary>
public sealed class CodificacaoFerramentasTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CodificacaoFerramentas _codificacao;
    private readonly Dictionary<string, int> _maquinas = new();

    public CodificacaoFerramentasTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        foreach (var codigo in new[] { "B1", "B2", "B3", "C1", "C2", "C3" })
        {
            var maquina = new Machine { Code = codigo };
            _db.Machines.Add(maquina);
            _db.SaveChanges();
            _maquinas[codigo] = maquina.Id;
        }

        _codificacao = new CodificacaoFerramentas(_db);
    }

    public void Dispose() => _db.Dispose();

    private Tool Ferramenta(string id, ToolType tipo, string referencia, params string[] maquinas)
    {
        var tool = new Tool { ToolId = id, Type = tipo, Reference = referencia, Lot = "1" };
        _db.Tools.Add(tool);
        foreach (var maquina in maquinas)
            _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = id, MachineId = _maquinas[maquina] });
        _db.SaveChanges();
        return tool;
    }

    /// <summary>Garante: sem nada gravado, a tabela mostra a folha da MG (B1 NNPB, B2 Dupla e Tripla, AN) e a B2 começa em Dupla.</summary>
    [Fact]
    public async Task Sem_nada_gravado_a_tabela_e_a_folha_da_MG()
    {
        var tabela = await _codificacao.LerAsync();

        Assert.Equal(B2Modo.Dupla, tabela.ModoB2);
        Assert.Equal(new[] { "B1", "B2 Dupla", "B2 Tripla", "B3", "C1", "C2", "C3" }, tabela.Filas.Select(f => f.Linha.Nome));
        var b1 = tabela.Fila(Codificacao.B1)!;
        Assert.Equal("Mxxxx95MPNNX", b1.Celula("CM").Padrao);
        Assert.Equal("Mxxxx95MFNNVX", b1.Celula("MF").Padrao);
        Assert.Equal("ABxxxxNX34", b1.Celula("BQ").Padrao);
        Assert.Equal(new int?[] { 96, 72, 120, 180 }, b1.Celulas.Select(c => c.Quantidade));
        var tripla = tabela.Fila(Codificacao.B2Tripla)!;
        Assert.Equal("Mxxxx4TMFPSV", tripla.Celula("MF").Padrao);
        Assert.Equal(new int?[] { 72, 54, 90, 135 }, tripla.Celulas.Select(c => c.Quantidade));
    }

    /// <summary>Garante: os nomes técnicos são editáveis (NX passa a NZ), um campo apagado fica vazio e uma quantidade má fica de fora com aviso.</summary>
    [Fact]
    public async Task Guardar_muda_os_nomes_e_as_quantidades_e_deixa_de_fora_a_ma()
    {
        var outcome = await _codificacao.GuardarAsync(new Dictionary<string, string?>
        {
            [Codificacao.ChavePadrao("BQ", Codificacao.B1)] = " abxxxxnz34 ",
            [Codificacao.ChavePadrao("MF", Codificacao.B1)] = "",
            [Codificacao.ChaveQuantidade("CM", Codificacao.B1)] = "80",
            [Codificacao.ChaveQuantidade("MF", Codificacao.B1)] = "mau"
        });

        Assert.Equal(GuardarCodificacaoResultado.Guardado, outcome.Resultado);
        var ignorada = Assert.Single(outcome.Ignoradas);
        Assert.Equal(("MF", "B1", "mau"), (ignorada.Tipo, ignorada.Linha, ignorada.Escrito));
        var b1 = (await _codificacao.LerAsync()).Fila(Codificacao.B1)!;
        Assert.Equal("ABxxxxNZ34", b1.Celula("BQ").Padrao);
        Assert.Null(b1.Celula("MF").Padrao);
        Assert.Equal(80, b1.Celula("CM").Quantidade);
        Assert.Equal(72, b1.Celula("MF").Quantidade);

        var recusa = await _codificacao.GuardarAsync(new Dictionary<string, string?> { ["Ferramentas:Outra"] = "1" });
        Assert.Equal(GuardarCodificacaoResultado.ChaveDesconhecida, recusa.Resultado);
    }

    /// <summary>Garante: escolher B2 Tripla grava o modo e põe as quantidades da B2 Tripla no total da B2 que o Criar sugere.</summary>
    [Fact]
    public async Task O_modo_da_B2_poe_as_quantidades_desse_modo_na_B2()
    {
        await _codificacao.GuardarModoB2Async(B2Modo.Tripla);

        Assert.Equal(B2Modo.Tripla, (await _codificacao.LerAsync()).ModoB2);
        string? Valor(string chave) => _db.ModuleSettings.AsNoTracking().SingleOrDefault(s => s.SettingKey == chave)?.SettingValue;
        Assert.Equal("72", Valor(ToolLotQuantityKeys.For(ToolType.Cm, "B2")));
        Assert.Equal("90", Valor(ToolLotQuantityKeys.For(ToolType.Bq, "B2")));
    }

    /// <summary>Garante: a linha dá o nome técnico (xxxx = referência) e as quantidades; na B2 conta o modo, ou a C1/C3; só a B2 sem modo não dá nada.</summary>
    [Fact]
    public async Task Sugerir_pela_linha_e_pelo_modo_da_B2()
    {
        var b1 = await _codificacao.SugerirAsync(ToolType.Cm, "2032", [_maquinas["B1"]], null);
        Assert.Equal(("M203295MPNNX", 96), (b1!.NomeTecnico, b1.Quantidade));

        var bq = await _codificacao.SugerirAsync(ToolType.Bq, "C002", [_maquinas["C3"], _maquinas["B2"]], null);
        Assert.Equal(("ABC002PX77", 90, 135), (bq!.NomeTecnico, bq.Quantidade, bq.QuantidadeAn));

        var tripla = await _codificacao.SugerirAsync(ToolType.Mf, "1075", [_maquinas["B2"]], B2Modo.Tripla);
        Assert.Equal(("M10754TMFPSV", 54), (tripla!.NomeTecnico, tripla.Quantidade));

        Assert.Null(await _codificacao.SugerirAsync(ToolType.Cm, "1075", [_maquinas["B2"]], null));
    }

    /// <summary>
    /// Garante: o «Atualizar» preenche nomes e quantidades vazias, dá o modo
    /// da B2 pela C1/C3, troca os nomes vindos do padrão anterior e não mexe
    /// num nome escrito à mão.
    /// </summary>
    [Fact]
    public async Task Atualizar_aplica_a_tabela_as_ferramentas_registadas()
    {
        var vazia = Ferramenta("Tvazia", ToolType.Bq, "C142", "B1");
        var doPadrao = Ferramenta("Tpadrao", ToolType.Cm, "2032", "B1");
        doPadrao.TechnicalName = "M203295MPNNX";
        doPadrao.BqAccountedQuantity = 72;
        var aMao = Ferramenta("Tmao", ToolType.Mf, "2032", "B1");
        aMao.TechnicalName = "M203295MFPSV";
        aMao.BqAccountedQuantity = 54;
        var b2 = Ferramenta("Tb2", ToolType.Cm, "1075", "C3", "B2");
        Ferramenta("Tsemmodo", ToolType.Cm, "1076", "B2");
        _db.SaveChanges();

        Assert.Equal(2, await _codificacao.ContarAtualizarAsync());
        Assert.Equal(2, await _codificacao.AtualizarAsync());
        Assert.Equal(("ABC142NX34", 120, 180), (vazia.TechnicalName, vazia.BqAccountedQuantity, vazia.AnQuantity));
        Assert.Equal(("M107544MPPSX", 72, B2Modo.Tripla), (b2.TechnicalName, b2.BqAccountedQuantity, b2.B2Modo));
        Assert.Equal("M203295MFPSV", aMao.TechnicalName);
        Assert.Equal(0, await _codificacao.ContarAtualizarAsync());

        // O padrão muda (NNX passa a NZX): o «Atualizar» troca os nomes que vieram do anterior.
        await _codificacao.GuardarAsync(new Dictionary<string, string?> { [Codificacao.ChavePadrao("CM", Codificacao.B1)] = "Mxxxx95MPNZX" });
        Assert.Equal(1, await _codificacao.AtualizarAsync());
        Assert.Equal(("M203295MPNZX", 72), (doPadrao.TechnicalName, doPadrao.BqAccountedQuantity));
        Assert.Equal("M203295MFPSV", aMao.TechnicalName);
    }
}

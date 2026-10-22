using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A utilização pelo SAP (Diogo, 20–21/10/2026; blueprint
/// ferramentas/ESTADO-E-UTILIZACAO §2.2 e armazem/OVERVIEW §2): a lista do
/// SAP (o HTML da ZPPF ou o CSV do script da ZPPG) dá o % de cada Equipment
/// Code, que é o nome técnico seguido do lote. Cada linha acrescenta uma
/// leitura à ferramenta com esse nome técnico e lote e fecha o lembrete dela;
/// o lembrete abre-se com a Entrada no Armazém vinda da Produção, e só as
/// ferramentas com ele aberto vão nos códigos a pedir ao SAP.
/// </summary>
public sealed class FerramentasUsoSapTests
{
    // Linhas da exportação real do «Gestão de Moldes» (5447), com as colunas
    // que interessam e outras que não.
    private const string HtmlDoSap = """
        <html><body><table class="dd">
        <tr><th></th><th>Equipamento</th><th>Owner</th><th>Expected life</th><th>Consumption [million units]</th><th>Residual life</th><th>% Use</th><th>Batch Descripton</th></tr>
        <tr><td></td><td>M544744MPPSX11</td><td>MG</td><td>43.200.000</td><td>38.962.451</td><td>4.237.549</td><td>90,2</td><td>M544744MPPSX</td></tr>
        <tr><td></td><td>M544744MPPSX12</td><td>MG</td><td>43.200.000</td><td>22.727.250</td><td>20.472.750</td><td>52,6</td><td>M544744MPPSX</td></tr>
        <tr><td></td><td>M544744MPPSX13</td><td>MG</td><td>43.200.000</td><td>0</td><td>43.200.000</td><td>0,0</td><td>M544744MPPSX-A</td></tr>
        </table></body></html>
        """;

    // O CSV que o script da ZPPG grava (DMO_USO_SAP.vbs).
    private const string CsvDoScript = """
        search,equipment,percent_use,last_production,batch_description,status,error
        "M544744MPPSX11","M544744MPPSX11","91,4","21.10.2026","M544744MPPSX","FOUND",""
        "M544744MPPSX99","","","","","ERROR","Sem resultados"
        """;

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-uso-sap-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = "cm11", Type = ToolType.Cm, Reference = "5447T173", Lot = "11", TechnicalName = "M544744MPPSX" },
            new Tool { ToolId = "mf11", Type = ToolType.Mf, Reference = "5447T173", Lot = "11" },
            new Tool { ToolId = "cm12", Type = ToolType.Cm, Reference = "5447T173", Lot = "12", TechnicalName = "m5447.44mppsx" },
            new Tool { ToolId = "cm3", Type = ToolType.Cm, Reference = "5447T173", Lot = "3", TechnicalName = "M544744MPPSX" },
            // A linha DUMMY canónica, como a migração semeia (o Eliminar precisa dela).
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Id = 1, Codigo = "1033", CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static FerramentasUsoSap Servico(DmoDbContext db) => new(db, new Sessao());

    private static async Task LembreteAsync(DmoDbContext db, string toolId)
    {
        db.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = toolId, CriadoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public void AListaHtmlDoSap_DaOCodigoEOUsoDeCadaLinha()
    {
        var leitura = UsoSapExportacao.Ler(HtmlDoSap);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(
            new[]
            {
                new UsoSapLinha("M544744MPPSX11", 90.2m, 38_962_451, 43_200_000),
                new UsoSapLinha("M544744MPPSX12", 52.6m, 22_727_250, 43_200_000),
                new UsoSapLinha("M544744MPPSX13", 0m, 0, 43_200_000)
            },
            leitura.Linhas);
    }

    [Fact]
    public void OCsvDoScript_LeOPercDasLinhasEncontradasEDeixaAsOutras()
    {
        var leitura = UsoSapExportacao.Ler(CsvDoScript);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(new[] { new UsoSapLinha("M544744MPPSX11", 91.4m, null, null) }, leitura.Linhas);
    }

    [Fact]
    public void UmFicheiroQueNaoEALista_NaoTemCabecalho()
    {
        Assert.False(UsoSapExportacao.Ler("<html><table><tr><td>Outra coisa</td></tr></table></html>").CabecalhoEncontrado);
        Assert.False(UsoSapExportacao.Ler("").CabecalhoEncontrado);
    }

    [Theory]
    [InlineData("90,2", 90.2)]
    [InlineData("43.200.000", 43200000)]
    [InlineData("90.2", 90.2)]
    [InlineData("1.234", 1234)]
    public void OsNumerosDoSap_EmFormatoPortugues(string texto, double esperado)
    {
        Assert.True(UsoSapExportacao.Numero(texto, out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5-")]
    public void OQueNaoEUmNumero_FicaDeFora(string texto)
    {
        Assert.False(UsoSapExportacao.Numero(texto, out _));
    }

    [Theory]
    [InlineData("M544744MPPSX11", "M544744MPPSX", "11", true)]
    [InlineData("M544744MPPSX11", "M544744MPPSX", "011", true)]
    [InlineData("M544744MPPSX03", "M544744MPPSX", "3", true)]
    [InlineData("M544744MPPSX11", "M544744MPPSX", "12", false)]
    [InlineData("M544744MPPSX11", "M544744MPP", "11", false)]
    [InlineData("M544744MPPSX11", "M544744MPPSX", "s/lote", false)]
    [InlineData("M544744MPPSX11", null, "11", false)]
    public void ALinhaDoSap_EDaFerramentaComOMesmoNomeTecnicoELote(string codigo, string? nomeTecnico, string lote, bool corresponde)
    {
        Assert.Equal(corresponde, UsoSapCodigo.Corresponde(codigo, nomeTecnico, lote));
    }

    [Fact]
    public void OCodigoAPedirAoSap_EONomeTecnicoComOLoteEmDoisAlgarismos()
    {
        Assert.Equal("M544744MPPSX11", UsoSapCodigo.Da("M544744MPPSX", "11"));
        Assert.Equal("M544744MPPSX03", UsoSapCodigo.Da(" m5447.44mppsx ", "3"));
        Assert.Null(UsoSapCodigo.Da("M544744MPPSX", ToolRegistryConstants.NoLot));
        Assert.Null(UsoSapCodigo.Da(null, "11"));
    }

    [Fact]
    public async Task Importar_AcrescentaUmaLeituraPorFerramentaEDizOQueNaoTemFerramenta()
    {
        await using var db = CreateContext();
        var modificado = new DateTime(2026, 10, 21, 6, 0, 0, DateTimeKind.Utc);

        var outcome = await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", modificado);

        Assert.Equal(ImportarUsoSapResultado.Gravado, outcome.Resultado);
        Assert.Equal((3, 2), (outcome.Linhas, outcome.Atualizadas));
        Assert.Equal(new[] { "M544744MPPSX13" }, outcome.SemFerramenta);
        db.ChangeTracker.Clear();
        var leituras = await db.FerramentaUtilizacoes.AsNoTracking().OrderBy(u => u.ToolId).ToListAsync();
        Assert.Equal(new[] { "cm11", "cm12" }, leituras.Select(u => u.ToolId));
        var cm11 = leituras[0];
        Assert.Equal((90.2m, FerramentaUtilizacao.OrigemSap, "Rui"), (cm11.Percentagem, cm11.Origem, cm11.RegistadoPor));
        Assert.Equal((38_962_451L, 43_200_000L, "M544744MPPSX11"), (cm11.Consumo!.Value, cm11.VidaPrevista!.Value, cm11.CodigoEquipamento));
        Assert.Equal((modificado, "percentagem.HTML"), (cm11.FicheiroModificadoEmUtc!.Value, cm11.Ficheiro));
        Assert.Equal(52.6m, leituras[1].Percentagem);
    }

    [Fact]
    public async Task OMesmoValorOutraVez_NaoAcrescentaNadaEUmValorNovoAcrescenta()
    {
        await using var db = CreateContext();
        await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);
        db.ChangeTracker.Clear();

        var outraVez = await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);
        db.ChangeTracker.Clear();
        var novo = await Servico(db).ImportarAsync(CsvDoScript, "dmo-uso-sap.csv", null);

        Assert.Equal((0, 1), (outraVez.Atualizadas, novo.Atualizadas));
        db.ChangeTracker.Clear();
        var atual = await Servico(db).DaFerramentaAsync("cm11");
        Assert.Equal((91.4m, (long?)null), (atual.Ultima!.Percentagem, atual.Ultima.Consumo));
        Assert.Equal(3, await db.FerramentaUtilizacoes.CountAsync());
    }

    [Fact]
    public async Task ALeitura_FechaOLembreteMesmoComOMesmoValor()
    {
        await using var db = CreateContext();
        await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);
        await LembreteAsync(db, "cm11");

        var outcome = await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);

        Assert.Equal(1, outcome.Atualizadas);
        db.ChangeTracker.Clear();
        var lembrete = await db.FerramentaLembretesUtilizacao.AsNoTracking().SingleAsync();
        var leitura = await db.FerramentaUtilizacoes.AsNoTracking().Where(u => u.ToolId == "cm11").OrderByDescending(u => u.Id).FirstAsync();
        Assert.NotNull(lembrete.FechadoEmUtc);
        Assert.Equal(leitura.Id, lembrete.UtilizacaoId);
        Assert.Null((await Servico(db).DaFerramentaAsync("cm11")).LembreteDesdeUtc);
    }

    [Fact]
    public async Task UmFicheiroQueNaoEALista_NaoGravaNada()
    {
        await using var db = CreateContext();

        var outcome = await Servico(db).ImportarAsync("isto não é a lista", "notas.txt", null);

        Assert.Equal(ImportarUsoSapResultado.NaoELista, outcome.Resultado);
        Assert.Equal(0, await db.FerramentaUtilizacoes.CountAsync());
    }

    [Fact]
    public async Task OsCodigos_SaoSoOsDasFerramentasComOLembreteAberto()
    {
        await using var db = CreateContext();
        await LembreteAsync(db, "cm11");
        await LembreteAsync(db, "cm3");
        await LembreteAsync(db, "mf11");
        db.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = "cm12", CriadoEmUtc = DateTime.UtcNow, FechadoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var codigos = await Servico(db).CodigosAsync();

        Assert.Equal(new[] { "M544744MPPSX03", "M544744MPPSX11" }, codigos);
    }

    [Fact]
    public async Task Pedir_AbreUmLembreteSoUmaVez()
    {
        await using var db = CreateContext();
        var servico = Servico(db);

        Assert.True(await servico.PedirAsync("cm12"));
        Assert.True(await servico.PedirAsync("cm12"));
        Assert.False(await servico.PedirAsync("nao-existe"));

        db.ChangeTracker.Clear();
        var lembrete = await db.FerramentaLembretesUtilizacao.AsNoTracking().SingleAsync();
        Assert.Equal(("cm12", "Rui", (int?)null), (lembrete.ToolId, lembrete.CriadoPor, lembrete.MovimentoId));
        Assert.Equal(new[] { "M544744MPPSX12" }, await servico.CodigosAsync());
    }

    [Theory]
    [InlineData(ArmazemOrigem.Producao, true)]
    [InlineData(ArmazemOrigem.Reparacao, false)]
    [InlineData(ArmazemOrigem.JaEstava, false)]
    public async Task AEntradaVindaDaProducao_AbreOLembreteDaFerramentaEDoPar(ArmazemOrigem origem, bool abre)
    {
        await using var db = CreateContext();
        var armazem = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());

        var saida = await armazem.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), "cm11", "1033", origem, FerramentaEstadoTipo.Reparado, null, null, ComPar: true));

        Assert.Equal(ArmazemMovimentoResultado.Gravado, saida.Resultado);
        db.ChangeTracker.Clear();
        var lembretes = await db.FerramentaLembretesUtilizacao.AsNoTracking().OrderBy(l => l.ToolId).ToListAsync();
        Assert.Equal(abre ? new[] { "cm11", "mf11" } : [], lembretes.Select(l => l.ToolId));
        Assert.All(lembretes, l => Assert.NotNull(l.MovimentoId));
    }

    [Fact]
    public async Task EliminarAFerramenta_LevaAUtilizacaoEOLembrete()
    {
        await using var db = CreateContext();
        await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);
        await LembreteAsync(db, "cm11");
        db.ChangeTracker.Clear();

        var outcome = await new DeleteToolHandler(db).HandleAsync(new DeleteTool("cm11"));

        Assert.Equal(DeleteToolResult.Success, outcome.Result);
        db.ChangeTracker.Clear();
        Assert.Equal(new[] { "cm12" }, await db.FerramentaUtilizacoes.AsNoTracking().Select(u => u.ToolId).ToListAsync());
        Assert.Equal(0, await db.FerramentaLembretesUtilizacao.CountAsync());
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }

    // O % uso escrito no Editar (Diogo, 22/10/2026): uma leitura manual que
    // fecha o lembrete; igual à última, negativo ou sem ferramenta não grava.
    [Fact]
    public async Task OPercentManual_GravaUmaLeituraEFechaOLembrete_IgualNaoGrava()
    {
        await using var db = CreateContext();
        await Servico(db).ImportarAsync(HtmlDoSap, "percentagem.HTML", null);
        await LembreteAsync(db, "cm11");

        Assert.False(await Servico(db).RegistarPercentagemManualAsync("cm11", 90));
        Assert.False(await Servico(db).RegistarPercentagemManualAsync("cm11", -1));
        Assert.False(await Servico(db).RegistarPercentagemManualAsync("nao-existe", 5));
        Assert.True(await Servico(db).RegistarPercentagemManualAsync("cm11", 95));

        db.ChangeTracker.Clear();
        var atual = await Servico(db).DaFerramentaAsync("cm11");
        Assert.Equal((FerramentaUtilizacao.OrigemManual, 95m, (long?)null), (atual.Ultima!.Origem, atual.Ultima.Percentagem, atual.Ultima.Consumo));
        Assert.Null(atual.LembreteDesdeUtc);
    }

    // O aviso do Sumário (Diogo, 22/10/2026): os lembretes abertos.
    [Fact]
    public async Task PorAtualizar_SaoOsLembretesAbertos_OsMaisAntigosPrimeiro()
    {
        await using var db = CreateContext();
        db.FerramentaLembretesUtilizacao.AddRange(
            new FerramentaLembreteUtilizacao { ToolId = "cm12", CriadoEmUtc = DateTime.UtcNow },
            new FerramentaLembreteUtilizacao { ToolId = "cm11", CriadoEmUtc = DateTime.UtcNow.AddDays(-1) },
            new FerramentaLembreteUtilizacao { ToolId = "cm3", CriadoEmUtc = DateTime.UtcNow.AddDays(-2), FechadoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var lista = await Servico(db).PorAtualizarAsync(10);

        Assert.Equal(["cm11", "cm12"], lista.Select(l => l.ToolId));
        Assert.Equal((ToolType.Cm, "5447T173", "11"), (lista[0].Tipo, lista[0].Referencia, lista[0].Lote));
        Assert.Single(await Servico(db).PorAtualizarAsync(1));
    }
}

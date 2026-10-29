using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// O fabrico pelo SAP (Diogo, 21/10/2026): o relatório «6- Manufacturing
/// Selected» dá, por material (a referência com o sufixo do SAP), linha e
/// datas, o que se produziu. Não tem ferramentas: liga-se ao Job On pela
/// referência, pela máquina e pelas datas, e a ferramenta vem do Job On (a
/// CM, o MF e a BQ). A ficha mostra os Job On da ferramenta com essa produção.
/// </summary>
public sealed class FerramentasFabricoSapTests
{
    // O início do relatório que o Diogo mandou (texto com tabulações, como o SAP o grava).
    private const string RelatorioDoSap =
        "        6- Manufacturing Selected\n" +
        "\tMaterial\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tCadência\tGob\tSectores\t     KCR\tAvg weight\t# weight\tGross Prod\t Net Prod.\tPack to pull\t Net Quant\t  Pallet\tTotal Rej.\tRej.MIL\tHotEnd%\n" +
        "\t5538T324BRA\tC3\tMG\t21.01.2025\t04.02.2025\t       14\t    542\t 3\t     12\t172,980\t      132\t   7\t1.404.234\t1.309.264\t      93,24\t9.918.720\t1.968,0\t     0,61\t  2,68\t 95,07\n" +
        "\t5538T324BRA\tC3\tMG\t31.07.2025\t06.08.2025\t        6\t    542\t 3\t     12\t172,980\t      132\t   7\t  615.060\t  571.360\t      92,90\t4.329.360\t  859,0\t     0,23\t  2,78\t 95,64\n" +
        "\t5538T324BRA\tC3\tMG\t29.09.2025\t07.10.2025\t        8\t    542\t 3\t     12\t172,324\t      131\t   6\t  811.515\t  748.183\t      92,20\t5.715.360\t1.134,0\t     1,32\t  3,16\t 95,80\n";

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-fabrico-sap-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 3, Code = "C3" }, new Machine { Id = 4, Code = "B1" });
        db.Tools.AddRange(
            new Tool { ToolId = "cm", Type = ToolType.Cm, Reference = "5538T324", Lot = "2" },
            new Tool { ToolId = "bq", Type = ToolType.Bq, Reference = "T324", Lot = "01" },
            new Tool { ToolId = "outra", Type = ToolType.Cm, Reference = "5538T324", Lot = "3" });
        db.JobOns.AddRange(
            JobOn(1, "202501", 3, new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4)),
            JobOn(2, "202502", 3, new DateOnly(2025, 7, 31), new DateOnly(2025, 8, 6)),
            // Na B1: o SAP só tem a C3, fica sem produção.
            JobOn(3, "202503", 4, new DateOnly(2025, 9, 29), new DateOnly(2025, 10, 7)),
            // Sem datas: nunca se liga ao SAP e fica no fim.
            JobOn(4, "202504", 3, null, null),
            JobOn(5, "202505", 3, new DateOnly(2025, 9, 29), new DateOnly(2025, 10, 7)));
        db.CmContexts.AddRange(
            new CmContext { JobOnId = 1, ToolId = "cm" },
            new CmContext { JobOnId = 3, ToolId = "cm" },
            new CmContext { JobOnId = 4, ToolId = "cm" },
            // Trocada a meio da produção 202502: também esteve nela.
            new CmContext { ReplacedJobOnId = 2, ReplacedAtUtc = DateTime.UtcNow, ToolId = "cm" },
            new CmContext { JobOnId = 5, ToolId = "outra" });
        db.BqContexts.Add(new BqContext { JobOnId = 5, ToolId = "bq" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static JobOn JobOn(int id, string producao, int maquina, DateOnly? inicio, DateOnly? fim) => new()
    {
        Id = id, Reference = "5538T324", ProductionNumber = producao, MachineId = maquina, InicioEm = inicio, FimEm = fim
    };

    private static FerramentasFabricoSap Servico(DmoDbContext db) => new(db, new Sessao(), Microsoft.Extensions.Logging.Abstractions.NullLogger<FerramentasFabricoSap>.Instance);

    [Fact]
    public void ORelatorioDoSap_DaOMaterialALinhaAsDatasEAProducao()
    {
        var leitura = FabricoSapExportacao.Ler(RelatorioDoSap);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(
            new[]
            {
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234, 1_309_264, 132m, 14),
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 7, 31), new DateOnly(2025, 8, 6), 615_060, 571_360, 132m, 6),
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 9, 29), new DateOnly(2025, 10, 7), 811_515, 748_183, 131m, 8)
            },
            leitura.Linhas);
    }

    // O relatório «produções» exportado em HTML (Diogo, 26/10/2026): MM,
    // Linha, DATA I/DATA F com espaço inseparável, QBRUTA, QUTIL e N_DIAS.
    [Fact]
    public void ORelatorioProducoes_DaAsGarrafasBrutasDeCadaProducao()
    {
        const string html = "<table><tr><td>MM</td><td>FABRICO</td><td>Linha</td><td>DATA&nbsp;I</td><td>DATA&nbsp;F</td><td>N_DIAS</td><td>Peso</td><td>QBRUTA</td><td>QUTIL</td></tr>"
            + "<tr><td>5227S368</td><td>202502</td><td>C2</td><td>24.11.2025</td><td>14.01.2026</td><td>52</td><td>130,072</td><td>45.999.205</td><td>41.298.660</td></tr></table>";

        var leitura = FabricoSapExportacao.Ler(html);

        Assert.True(leitura.CabecalhoEncontrado);
        var linha = Assert.Single(leitura.Linhas);
        Assert.Equal("5227S368", linha.Material);
        Assert.Equal("C2", linha.Linha);
        Assert.Equal(new DateOnly(2025, 11, 24), linha.InicioEm);
        Assert.Equal(new DateOnly(2026, 1, 14), linha.FimEm);
        Assert.Equal(45_999_205, linha.ProducaoBruta);
        Assert.Equal(41_298_660, linha.ProducaoLiquida);
        Assert.Equal(52, linha.DiasProducao);
    }

    [Fact]
    public void UmFicheiroSemOCabecalhoDoFabrico_NaoEORelatorio()
    {
        Assert.False(FabricoSapExportacao.Ler("Material\tLine\n5538T324BRA\tC3").CabecalhoEncontrado);
        Assert.False(FabricoSapExportacao.Ler(null).CabecalhoEncontrado);
    }

    [Theory]
    [InlineData("5538T324", "C3", "2025-01-21", "2025-02-04", true)]
    // SAP-B01 (auditoria de 29/10/2026): a linha é da produção cujo período
    // CONTÉM o início dela (21/01), não de qualquer uma que toque no período —
    // senão a mesma linha ligava a duas produções (esta e a de cima) e contava
    // a dobrar.
    [InlineData("5538T324", "c3", "2025-02-04", "2025-02-10", false)]
    // Sem fim, mas sem indicar a próxima produção na máquina (como aqui):
    // ainda na máquina, apanha as ordens do SAP desde o início.
    [InlineData("5538T324", "C3", "2025-01-01", null, true)]
    // SAP-B01: sem fim, mas a produção começa DEPOIS do início da linha
    // (21/01): não é dela — quem a contém é outra produção, com início
    // anterior ou igual ao da linha.
    [InlineData("5538T324", "C3", "2025-01-25", null, false)]
    [InlineData("5538T324", "C3", "2025-02-05", null, false)]
    // O início do SAP (21/01): um Job On que acaba antes não liga; um que acaba nesse dia liga.
    [InlineData("5538T324", "C3", "2025-01-10", "2025-01-20", false)]
    [InlineData("5538T324", "C3", "2025-01-10", "2025-01-21", true)]
    [InlineData("5538T324", "B1", "2025-01-21", "2025-02-04", false)]
    [InlineData("6012A118", "C3", "2025-01-21", "2025-02-04", false)]
    [InlineData("5538T324", "C3", "2025-02-05", "2025-02-10", false)]
    public void ALinhaDoSap_LigaSeAoJobOnPelaReferenciaMaquinaEDatas(string referencia, string maquina, string inicio, string? fim, bool liga)
    {
        Assert.Equal(liga, FabricoSapExportacao.Corresponde(
            "5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4),
            referencia, maquina, DateOnly.Parse(inicio), fim is null ? null : DateOnly.Parse(fim)));
    }

    [Fact]
    public void SemDataDeInicio_NaoSeLiga()
    {
        Assert.False(FabricoSapExportacao.Corresponde("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), "5538T324", "C3", null, null));
    }

    // SAP-B01 (auditoria de 29/10/2026): a produção A (21/01, sem fim) já não
    // engole a linha de fevereiro só porque a sua janela não tem fim — o
    // início da próxima produção na mesma máquina (B, 05/02) limita-a ao dia
    // anterior (04/02). A liga só à linha de janeiro; B só à de fevereiro.
    [Fact]
    public void ProducaoSemFim_ComProximaNaMesmaMaquina_NaoEngoleALinhaDaSeguinte()
    {
        var linhaJaneiro = (Material: "5538T324BRA", Linha: "C3", Inicio: new DateOnly(2025, 1, 21), Fim: new DateOnly(2025, 2, 4));
        var linhaFevereiro = (Material: "5538T324BRA", Linha: "C3", Inicio: new DateOnly(2025, 2, 5), Fim: new DateOnly(2025, 2, 10));
        var proximaProducao = new DateOnly(2025, 2, 5);

        Assert.True(FabricoSapExportacao.Corresponde(linhaJaneiro.Material, linhaJaneiro.Linha, linhaJaneiro.Inicio, linhaJaneiro.Fim,
            "5538T324", "C3", new DateOnly(2025, 1, 21), null, proximaProducao));
        Assert.False(FabricoSapExportacao.Corresponde(linhaFevereiro.Material, linhaFevereiro.Linha, linhaFevereiro.Inicio, linhaFevereiro.Fim,
            "5538T324", "C3", new DateOnly(2025, 1, 21), null, proximaProducao));
        Assert.True(FabricoSapExportacao.Corresponde(linhaFevereiro.Material, linhaFevereiro.Linha, linhaFevereiro.Inicio, linhaFevereiro.Fim,
            "5538T324", "C3", new DateOnly(2025, 2, 5), new DateOnly(2025, 2, 10)));
    }

    // SAP-B01: uma produção que acaba a meio de uma linha do SAP (21/01–25/01,
    // a linha vai até 04/02) continua a ligar — a janela dela CONTÉM o início
    // da linha — e leva a linha inteira; não há rateio por dia.
    [Fact]
    public void ProducaoQueAcabaAMeioDaLinha_LigaELevaALinhaInteira()
    {
        Assert.True(FabricoSapExportacao.Corresponde("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4),
            "5538T324", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 1, 25)));
    }

    // SAP-B03 (auditoria de 29/10/2026): o material só é da referência quando
    // o prefixo acaba o material ou o carácter a seguir não é um dígito (a
    // mudança mínima do relatório) — «5538T32» é um prefixo truncado de
    // «5538T324» (o carácter a seguir, «4», é um dígito: não seria fronteira).
    // «5538» já tem um carácter não-dígito a seguir («T»): fica a corresponder,
    // como o sufixo do país (BRA) depois da referência completa.
    [Theory]
    [InlineData("5538T324BRA", "5538T324", true)]
    [InlineData("5538T324BRA", "5538T32", false)]
    [InlineData("5538T324BRA", "5538", true)]
    [InlineData("5538T324BRA", "5538T324BRA", true)]
    public void ReferenciaCorresponde_ExigeFronteiraAposOPrefixo(string material, string referencia, bool corresponde) =>
        Assert.Equal(corresponde, FabricoSapExportacao.ReferenciaCorresponde(material, referencia));

    [Fact]
    public async Task LerOMesmoRelatorioOutraVez_AtualizaAsMesmasLinhas()
    {
        using var db = CreateContext();
        var linhas = FabricoSapExportacao.Ler(RelatorioDoSap).Linhas;

        var primeira = await Servico(db).ImportarAsync(linhas, "fabrico.txt", new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc));
        var corrigida = linhas.Select((l, i) => i == 0 ? l with { ProducaoBruta = 1_500_000 } : l).ToList();
        var segunda = await Servico(db).ImportarAsync(corrigida, "fabrico.txt", new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new ImportarFabricoSapOutcome(3, 3), primeira);
        Assert.Equal(new ImportarFabricoSapOutcome(3, 0), segunda);
        Assert.Equal(3, await db.SapFabricos.CountAsync());
        Assert.Equal(1_500_000, (await db.SapFabricos.SingleAsync(s => s.InicioEm == new DateOnly(2025, 1, 21))).ProducaoBruta);
        Assert.Equal(new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc), await Servico(db).UltimoFicheiroAsync());
        Assert.Equal("Rui", (await db.SapFabricos.FirstAsync()).LidoPor);
    }

    // SAP-B02 (auditoria de 29/10/2026): reimportar a mesma linha com um
    // relatório parcial (aqui só com produção líquida, sem peso nem produção
    // bruta) não pode apagar o que já estava gravado — um `null` do relatório
    // novo é «esta coluna não vem aqui», não «apaga o que já lá estava».
    [Fact]
    public async Task ReimportarComRelatorioParcial_NaoApagaOQueJaEstavaGravado()
    {
        using var db = CreateContext();
        var linhas = FabricoSapExportacao.Ler(RelatorioDoSap).Linhas;
        await Servico(db).ImportarAsync(linhas, "zfmd.txt", new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc));

        var parcial = linhas.Select(l => l with { ProducaoBruta = null, PesoMedioGramas = null, DiasProducao = null, ProducaoLiquida = l.ProducaoLiquida + 1 }).ToList();
        await Servico(db).ImportarAsync(parcial, "producoes.txt", new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc));

        var linha = await db.SapFabricos.SingleAsync(s => s.InicioEm == new DateOnly(2025, 1, 21));
        Assert.Equal(1_404_234, linha.ProducaoBruta);
        Assert.Equal(132m, linha.PesoMedioGramas);
        Assert.Equal(14, linha.DiasProducao);
        Assert.Equal(1_309_265, linha.ProducaoLiquida);
    }

    [Fact]
    public async Task AsProducoesDaFerramenta_SaoOsJobOnDelaComOQueOSapDiz()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);

        var producoes = await Servico(db).ProducoesAsync("cm", null, 10);

        // As mais recentes primeiro; a sem datas no fim; a 202505 é de outra CM.
        Assert.Equal(new[] { "202503", "202502", "202501", "202504" }, producoes.Pagina.Items.Select(p => p.Producao));
        var porProducao = producoes.Pagina.Items.ToDictionary(p => p.Producao);
        Assert.Equal((1_404_234L, 1_309_264L, 1), (porProducao["202501"].ProducaoBruta!.Value, porProducao["202501"].ProducaoLiquida!.Value, porProducao["202501"].LinhasSap));
        Assert.Equal(615_060L, porProducao["202502"].ProducaoBruta);
        Assert.Equal(((long?)null, 0), (porProducao["202503"].ProducaoBruta, porProducao["202503"].LinhasSap));
        Assert.Equal(("B1", "C3"), (porProducao["202503"].Maquina, porProducao["202504"].Maquina));
        Assert.Equal(((long?)null, 0), (porProducao["202504"].ProducaoLiquida, porProducao["202504"].LinhasSap));
    }

    [Fact]
    public async Task ABoquilhaTambemTemAsProducoes_EAListaPagina()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);

        var bq = await Servico(db).ProducoesAsync("bq", null, 10);
        var pagina2 = await Servico(db).ProducoesAsync("cm", 2, 3);

        Assert.Equal(811_515L, Assert.Single(bq.Pagina.Items).ProducaoBruta);
        Assert.Equal((4, 2, "202504"), (pagina2.Pagina.TotalCount, pagina2.Pagina.PageNumber, Assert.Single(pagina2.Pagina.Items).Producao));
        Assert.Equal(0, (await Servico(db).ProducoesAsync("nenhuma", null, 10)).Pagina.TotalCount);
    }

    [Fact]
    public async Task ODelta_EQuantoAUtilizacaoSubiuEmCadaProducao_EOTotalSomaTodas()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);
        // As leituras ao meio-dia (Lisboa) de cada dia.
        db.FerramentaUtilizacoes.AddRange(
            Leitura(1, new DateOnly(2025, 1, 15), 10m),
            Leitura(2, new DateOnly(2025, 2, 5), 15m),
            Leitura(3, new DateOnly(2025, 8, 6), 18.5m),
            Leitura(4, new DateOnly(2025, 1, 21), 12m));
        await db.SaveChangesAsync();

        var producoes = await Servico(db).ProducoesAsync("cm", null, 10);

        var delta = producoes.Pagina.Items.ToDictionary(p => p.Producao, p => p.DeltaUtilizacao);
        // 202501: 12 no próprio dia do início (conta como antes, é o ponto de
        // partida), 15 depois de 04/02. 202502: 15 antes de 31/07, 18,5 no dia do fim.
        Assert.Equal(3m, delta["202501"]);
        Assert.Equal(3.5m, delta["202502"]);
        // 202503: sem leitura depois do fim; 202504: sem datas.
        Assert.Null(delta["202503"]);
        Assert.Null(delta["202504"]);
        Assert.Equal((1_404_234L + 615_060L, 1_309_264L + 571_360L), (producoes.TotalBruta!.Value, producoes.TotalLiquida!.Value));
    }

    [Fact]
    public async Task ALeituraDeHoje_SoEOUsoDepoisDaUltimaProducao_NaoDasAnteriores()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);
        // Só uma leitura manual, depois de todas (Diogo, 27/10/2026: «sempre 16% uso?»).
        db.FerramentaUtilizacoes.Add(Leitura(1, new DateOnly(2025, 10, 10), 16m));
        await db.SaveChangesAsync();

        var producoes = await Servico(db).ProducoesAsync("cm", null, 10);
        var depois = producoes.Pagina.Items.ToDictionary(p => p.Producao, p => p.UtilizacaoDepois);
        var historico = (await Servico(db).ProducoesDeFerramentasAsync(["cm"], null))["cm"].ToDictionary(p => p.Producao, p => p.UtilizacaoDepois);

        // A 202503 acabou antes da leitura; as outras deram lugar à seguinte antes dela.
        Assert.Equal(16m, depois["202503"]);
        Assert.Null(depois["202502"]);
        Assert.Null(depois["202501"]);
        Assert.Equal(depois.OrderBy(k => k.Key), historico.OrderBy(k => k.Key));
    }

    [Fact]
    public async Task OTotal_ContaTodasAsProducoes_NaoSoAPagina()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);

        var pagina = await Servico(db).ProducoesAsync("cm", 1, 1);

        Assert.Equal("202503", Assert.Single(pagina.Pagina.Items).Producao);
        Assert.Equal(1_404_234L + 615_060L, pagina.TotalBruta);
    }

    private static SapFabrico Ordem(string inicio, string fim, decimal? peso) => new()
    {
        Material = "5538T324BRA", Linha = "C3", InicioEm = DateOnly.Parse(inicio), FimEm = DateOnly.Parse(fim), PesoMedioGramas = peso
    };

    [Fact]
    public void OPesoDoSap_EAMediaPelosDiasEAModaOPesoComMaisDias()
    {
        // 132 g em 14 + 6 dias, 131 g em 8 (a ordem sem peso, dentro do Job On, não conta): média (132 × 20 + 131 × 8) / 28; moda 132, usada 20 dos 28 dias.
        var resumo = FabricoSapPeso.Resumo(
            [Ordem("2025-01-21", "2025-02-04", 132m), Ordem("2025-02-04", "2025-02-12", 131m), Ordem("2025-02-12", "2025-02-18", 132m), Ordem("2025-02-14", "2025-02-18", null)],
            new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 18));

        Assert.Equal(new PesoSapResumo(131.71m, 132m, 28, 3, ModaDias: 20), resumo);
    }

    [Fact]
    public void OsDiasDeCadaOrdem_SaoOsQueFicamDentroDoJobOn_EAModaEmpatadaEAMaisRecente()
    {
        // O Job On começa a 25/01: a 1.ª ordem conta 10 dias (25/01 a 04/02), a 2.ª também 10.
        var resumo = FabricoSapPeso.Resumo(
            [Ordem("2025-01-21", "2025-02-04", 132m), Ordem("2025-02-04", "2025-02-14", 130m)],
            new DateOnly(2025, 1, 25), new DateOnly(2025, 2, 14));

        Assert.Equal(new PesoSapResumo(131m, 130m, 20, 2, ModaDias: 10), resumo);
        Assert.Null(FabricoSapPeso.Resumo([Ordem("2025-01-21", "2025-02-04", null)], new DateOnly(2025, 1, 21), null));
    }

    [Fact]
    public async Task OPesoDosJobOns_VemDasOrdensDaMesmaReferenciaMaquinaEDatas()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);

        var pesos = await Servico(db).PesoDosJobOnsAsync([1, 3, 4, 5]);

        Assert.Equal(new PesoSapResumo(132m, 132m, 14, 1, ModaDias: 14), pesos[1]);
        // 202505 (C3, 29/09 a 07/10): a ordem de 131 g, 8 dias.
        Assert.Equal(new PesoSapResumo(131m, 131m, 8, 1, ModaDias: 8), pesos[5]);
        // Na B1 e sem datas: sem peso.
        Assert.False(pesos.ContainsKey(3));
        Assert.False(pesos.ContainsKey(4));
    }

    [Fact]
    public async Task ORelatorioProducoesReal_LigaAsProducoesDaCmDaB1_ComGarrafas()
    {
        // O ficheiro do PC da fábrica: BOM, fim de linha do Windows, «Line» sem tabulação à frente.
        const string Real = "\uFEFFMaterial\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tCadência\tGob\tSectores\tKCR\tAvg weight\tΔ weight\tGross Prod\tNet Prod.\tPack to pull\tNet Quant\tPallet\tTotal Rej.\tRej.MIL\tHotEnd%\r\n" +
            "1075C142BRA\tB1\tMG\t29.07.2026\t04.08.2026\t6\t636\t4\t12\t159,551\t145\t5\t784.875\t668.273\t85,14\t4.629.836\t1.173,0\t2,22\t9,30\t95,29\r\n" +
            "1075C142BRA\tB1\tMG\t19.10.2026\t25.10.2026\t6\t636\t4\t12\t158,447\t143\t3\t907.617\t766.203\t84,42\t5.339.476\t1.548,0\t4,13\t8,20\t94,90\r\n";
        using var db = CreateContext();
        db.Tools.Add(new Tool { ToolId = "st100", Type = ToolType.Cm, Reference = "1075C142", Lot = "1" });
        db.JobOns.AddRange(
            new JobOn { Id = 10, Reference = "1075C142", ProductionNumber = "202603", MachineId = 4, InicioEm = new DateOnly(2026, 7, 29), FimEm = new DateOnly(2026, 8, 4) },
            new JobOn { Id = 11, Reference = "1075C142", ProductionNumber = "202604", MachineId = 4, InicioEm = new DateOnly(2026, 10, 19), FimEm = null });
        db.CmContexts.AddRange(new CmContext { JobOnId = 10, ToolId = "st100" }, new CmContext { JobOnId = 11, ToolId = "st100" });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(Real).Linhas, "dmo-fabrico-sap.txt", DateTime.UtcNow);

        var producoes = await Servico(db).ProducoesAsync("st100", null, 10);

        Assert.Equal(new long?[] { 907_617, 784_875 }, producoes.Pagina.Items.Select(p => p.ProducaoBruta));
    }

    // SAP-03 (sweep Codex 29/10/2026): uma ordem anulada ou corrigida no SAP
    // desaparece da ZFMD; o DMO não pode continuar a contá-la para sempre.
    [Fact]
    public async Task UmaOrdemQueDeixaDeVirNoRelatorio_EApagadaDaJanelaDoRelatorio()
    {
        var db = CreateContext();
        var a = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234, 1_309_264, 132m, 14);
        var b = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 10), 615_060, 571_360, 132m, 6);
        var c = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 7, 31), new DateOnly(2025, 8, 6), 811_515, 748_183, 131m, 8);
        await Servico(db).ImportarAsync([a, b, c], "dmo-fabrico-sap-20260101-0900.txt", DateTime.UtcNow);
        Assert.Equal(3, await db.SapFabricos.CountAsync());

        // O relatório seguinte ainda cobre a mesma janela (tem «a» e «c», antes e depois de «b»),
        // mas já não traz «b»: foi anulada ou corrigida no SAP.
        var outcome = await Servico(db).ImportarAsync([a, c], "dmo-fabrico-sap-20260102-0900.txt", DateTime.UtcNow);

        Assert.Equal(new ImportarFabricoSapOutcome(2, 0, 1), outcome);
        var restantes = await db.SapFabricos.AsNoTracking().Select(s => s.InicioEm).OrderBy(d => d).ToListAsync();
        Assert.Equal([a.InicioEm, c.InicioEm], restantes);
    }

    // Uma ordem fora da janela do relatório novo (antes do início lido agora) não é tocada.
    [Fact]
    public async Task UmaOrdemForaDaJanelaDoRelatorioNovo_NaoEApagada()
    {
        var db = CreateContext();
        var antiga = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 10), 100_000, 90_000, 132m, 9);
        await Servico(db).ImportarAsync([antiga], "dmo-fabrico-sap-antigo.txt", DateTime.UtcNow);

        var nova = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234, 1_309_264, 132m, 14);
        var outcome = await Servico(db).ImportarAsync([nova], "dmo-fabrico-sap-novo.txt", DateTime.UtcNow);

        Assert.Equal(0, outcome.Retiradas);
        Assert.Equal(2, await db.SapFabricos.CountAsync());
    }

    // Duas linhas distintas do relatório colapsadas na mesma chave: fica a última, sem apagar nada.
    [Fact]
    public async Task DuasLinhasComAMesmaChave_FicamColapsadasNumaSoSemApagarNada()
    {
        var db = CreateContext();
        var primeira = new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_000, 900, 132m, 14);
        var segunda = primeira with { ProducaoBruta = 2_000, ProducaoLiquida = 1_900 };

        var outcome = await Servico(db).ImportarAsync([primeira, segunda], "dmo-fabrico-sap-colisao.txt", DateTime.UtcNow);

        Assert.Equal(new ImportarFabricoSapOutcome(1, 1, 0), outcome);
        var gravada = await db.SapFabricos.AsNoTracking().SingleAsync();
        Assert.Equal(2_000, gravada.ProducaoBruta);
    }

    private static FerramentaUtilizacao Leitura(long id, DateOnly dia, decimal percentagem) => new()
    {
        Id = id, ToolId = "cm", Percentagem = percentagem,
        RegistadaEmUtc = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(11, 0)), DateTimeKind.Utc)
    };

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}

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

    private static FerramentasFabricoSap Servico(DmoDbContext db) => new(db, new Sessao());

    [Fact]
    public void ORelatorioDoSap_DaOMaterialALinhaAsDatasEAProducao()
    {
        var leitura = FabricoSapExportacao.Ler(RelatorioDoSap);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(
            new[]
            {
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234, 1_309_264),
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 7, 31), new DateOnly(2025, 8, 6), 615_060, 571_360),
                new FabricoSapLinha("5538T324BRA", "C3", new DateOnly(2025, 9, 29), new DateOnly(2025, 10, 7), 811_515, 748_183)
            },
            leitura.Linhas);
    }

    [Fact]
    public void UmFicheiroSemOCabecalhoDoFabrico_NaoEORelatorio()
    {
        Assert.False(FabricoSapExportacao.Ler("Material\tLine\n5538T324BRA\tC3").CabecalhoEncontrado);
        Assert.False(FabricoSapExportacao.Ler(null).CabecalhoEncontrado);
    }

    [Theory]
    [InlineData("5538T324", "C3", "2025-01-21", "2025-02-04", true)]
    [InlineData("5538T324", "c3", "2025-02-04", "2025-02-10", true)]
    // Sem fim: ainda na máquina, apanha as ordens do SAP desde o início.
    [InlineData("5538T324", "C3", "2025-01-01", null, true)]
    [InlineData("5538T324", "C3", "2025-01-25", null, true)]
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

    [Fact]
    public async Task AsProducoesDaFerramenta_SaoOsJobOnDelaComOQueOSapDiz()
    {
        using var db = CreateContext();
        await Servico(db).ImportarAsync(FabricoSapExportacao.Ler(RelatorioDoSap).Linhas, "fabrico.txt", DateTime.UtcNow);

        var producoes = await Servico(db).ProducoesAsync("cm", null, 10);

        // As mais recentes primeiro; a sem datas no fim; a 202505 é de outra CM.
        Assert.Equal(new[] { "202503", "202502", "202501", "202504" }, producoes.Items.Select(p => p.Producao));
        var porProducao = producoes.Items.ToDictionary(p => p.Producao);
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

        Assert.Equal(811_515L, Assert.Single(bq.Items).ProducaoBruta);
        Assert.Equal((4, 2, "202504"), (pagina2.TotalCount, pagina2.PageNumber, Assert.Single(pagina2.Items).Producao));
        Assert.Equal(0, (await Servico(db).ProducoesAsync("nenhuma", null, 10)).TotalCount);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}

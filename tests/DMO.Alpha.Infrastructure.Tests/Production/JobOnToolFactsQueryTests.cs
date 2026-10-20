using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova os FATOS TÉCNICOS das ferramentas na escolha da ficha (o
/// showToolFacts do dmo-html portado) e os AVISOS INFORMATIVOS que nunca
/// bloqueiam: a compatibilidade da máquina registada na ficha da tool
/// («compatibilidade registada B1, C3; confirme a máquina escolhida») e o
/// uso paralelo noutra produção ATIVA («também está associado a 202601
/// (B1). Esta informação não impede a sua escolha») — ambos calculados
/// PELO BACKEND sobre os factos persistidos; uma produção que não começou
/// ou já terminou não avisa; a produção em edição exclui-se a si própria.
/// </summary>
public sealed class JobOnToolFactsQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly JobOnToolFactsQuery _query;
    private static DateOnly Hoje => DateOnly.FromDateTime(DmoBusinessTime.Now);

    public JobOnToolFactsQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new JobOnToolFactsQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Machine> SeedMachineAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<Tool> SeedToolAsync(string toolId, ToolType type, string reference, string lot, int? quantidade = null)
    {
        var tool = new Tool { ToolId = toolId, Type = type, Reference = reference, Lot = lot, BqAccountedQuantity = quantidade };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task SeedTechnicalAsync(string toolId, CmProcess? process = null, decimal? volumePuncao = null, decimal? volumeMarisa = null, decimal? gargalo = null, decimal? tpD = null, decimal? tpR = null, decimal? tpH = null)
    {
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = toolId,
            Process = process,
            VolumePuncaoCm3 = volumePuncao,
            VolumeMarisaCm3 = volumeMarisa,
            DiametroGargaloMm = gargalo,
            TpDiametroMm = tpD,
            TpRaioMm = tpR,
            TpSagittaMm = tpH
        });
        await _db.SaveChangesAsync();
    }

    private async Task<int> SeedProducaoAtivaAsync(Machine machine, string? cmToolId, string? mfToolId, string? bqToolId, DateOnly? inicioEm, DateOnly? fimEm, string productionNumber)
    {
        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = productionNumber,
            Machine = machine,
            InicioEm = inicioEm,
            FimEm = fimEm
        };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        if (cmToolId is not null)
        {
            _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = cmToolId });
        }

        if (mfToolId is not null)
        {
            _db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = mfToolId });
        }

        if (bqToolId is not null)
        {
            _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = bqToolId });
        }

        await _db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task FindAsync_FatosTecnicosDaTool_OQueOsControlosConsomem()
    {
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3", CmProcess.Nnpb, volumePuncao: 30m, gargalo: 4.5m, tpD: 50m, tpR: 24.11m, tpH: 10m);
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq, "T194", "12", quantidade: 800);
        await SeedTechnicalAsync("BQ-TOOL-9", volumeMarisa: 10m, gargalo: 1.8m);
        await SeedToolAsync("MF-TOOL-9", ToolType.Mf, "5810", "1");
        await SeedTechnicalAsync("MF-TOOL-9", gargalo: 12.5m);

        var facts = await _query.FindAsync(new[] { "T3", "BQ-TOOL-9", "MF-TOOL-9" }, null, null, null, null);

        Assert.Equal(3, facts.Count);
        var cm = facts.Single(f => f.ToolId == "T3");
        Assert.Equal("CM", cm.Type);
        Assert.Equal("NNPB", cm.Processo);
        Assert.Equal(30m, cm.VolumePuncaoCm3);
        Assert.Equal(4.5m, cm.DiametroGargaloMm);
        Assert.Equal(50m, cm.TpDiametroMm);
        Assert.Equal(24.11m, cm.TpRaioMm);
        Assert.Equal(10m, cm.TpSagittaMm);

        var bq = facts.Single(f => f.ToolId == "BQ-TOOL-9");
        Assert.Equal("BQ", bq.Type);
        Assert.Equal(10m, bq.VolumeMarisaCm3);
        Assert.Equal(1.8m, bq.DiametroGargaloMm);
        Assert.Equal(800, bq.Quantidade);

        var mf = facts.Single(f => f.ToolId == "MF-TOOL-9");
        Assert.Equal("MF", mf.Type);
        Assert.Equal(12.5m, mf.DiametroGargaloMm);
    }

    [Fact]
    public async Task FindAsync_CompatibilidadeRegistada_AvisoSemBloqueio()
    {
        // A tool registada para B1 e C3, a ficha escolhe B2: o aviso
        // informativo explica e pede a confirmação — NUNCA um bloqueio.
        var b1 = await SeedMachineAsync("B1");
        var _b2 = await SeedMachineAsync("B2");
        var c3 = await SeedMachineAsync("C3");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", MachineId = b1.Id });
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", MachineId = c3.Id });
        await _db.SaveChangesAsync();

        var facts = await _query.FindAsync(new[] { "T3" }, "B2", null, null, null);

        var aviso = Assert.Single(facts).AvisoCompatibilidade;
        Assert.NotNull(aviso);
        Assert.Contains("CM 5447: compatibilidade registada B1, C3", aviso);
        Assert.Contains("confirme a máquina escolhida", aviso);
    }

    [Fact]
    public async Task FindAsync_MaquinaCompativel_SemAviso()
    {
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", MachineId = b1.Id });
        await _db.SaveChangesAsync();

        var facts = await _query.FindAsync(new[] { "T3" }, "B1", null, null, null);

        Assert.Null(Assert.Single(facts).AvisoCompatibilidade);
    }

    [Fact]
    public async Task FindAsync_SemMaquinasRegistadas_SemAviso()
    {
        // Sem links não há informação de compatibilidade: sem aviso (o
        // padrão da casa — função antes de proteção; a ficha completa-se
        // depois).
        await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");

        var facts = await _query.FindAsync(new[] { "T3" }, "B1", null, null, null);

        Assert.Null(Assert.Single(facts).AvisoCompatibilidade);
    }

    [Fact]
    public async Task FindAsync_UsoParalelo_ProducaoAtiva_AvisaSemBloqueio()
    {
        // A outra produção em aberto, a cruzar as datas desta, com a mesma tool:
        // o aviso «também está associado a 202601 (B1). Esta informação
        // não impede a sua escolha.»
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        var outra = await SeedProducaoAtivaAsync(
            machine, "T3", null, null,
            DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3), null, "202601");

        var facts = await _query.FindAsync(new[] { "T3" }, null, null, Hoje.AddDays(-3), null);

        var aviso = Assert.Single(facts).AvisoUsoParalelo;
        Assert.NotNull(aviso);
        Assert.Contains("CM 5447 / lote 3 também está associado a 202601 (B1)", aviso);
        Assert.Contains("Esta informação não impede a sua escolha", aviso);
    }

    [Fact]
    public async Task FindAsync_DatasQueNaoSeCruzam_NaoAvisa()
    {
        // A mesma ferramenta pode servir outra produção; só não pode
        // trabalhar ao mesmo tempo (o proprietário, 17/10/2026). Uma
        // produção SEM dia e outra que TERMINOU antes do início desta não
        // avisam; sem datas nesta também não há nada a comparar.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        await SeedProducaoAtivaAsync(machine, "T3", null, null, null, null, "202600");           // sem dia
        await SeedProducaoAtivaAsync(
            machine, "T3", null, null, Hoje.AddDays(-10), Hoje.AddDays(-4), "202599");            // terminou antes

        var facts = await _query.FindAsync(new[] { "T3" }, null, null, Hoje.AddDays(-3), null);
        Assert.Null(Assert.Single(facts).AvisoUsoParalelo);

        var semDatas = await _query.FindAsync(new[] { "T3" }, null, null, null, null);
        Assert.Null(Assert.Single(semDatas).AvisoUsoParalelo);
    }

    [Fact]
    public async Task FindAsync_DatasQueSeCruzam_AvisaMesmoNoFuturoOuJaTerminada()
    {
        // A sobreposição dos períodos é o que conta, não o dia de hoje:
        // uma produção planeada para a próxima semana cruza uma em aberto,
        // e duas já terminadas que partilharam dias também.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        await SeedProducaoAtivaAsync(machine, "T3", null, null, Hoje.AddDays(7), Hoje.AddDays(9), "202610");
        await SeedProducaoAtivaAsync(machine, "T3", null, null, Hoje.AddDays(-20), Hoje.AddDays(-15), "202590");

        var emAberto = await _query.FindAsync(new[] { "T3" }, null, null, Hoje, null);
        Assert.Contains("202610 (B1)", Assert.Single(emAberto).AvisoUsoParalelo);
        Assert.DoesNotContain("202590", Assert.Single(emAberto).AvisoUsoParalelo);

        var passada = await _query.FindAsync(new[] { "T3" }, null, null, Hoje.AddDays(-16), Hoje.AddDays(-12));
        Assert.Contains("202590 (B1)", Assert.Single(passada).AvisoUsoParalelo);
        Assert.DoesNotContain("202610", Assert.Single(passada).AvisoUsoParalelo);
    }

    [Fact]
    public async Task FindAsync_APropriaProducao_NaoContaComoUsoParalelo()
    {
        // A produção em edição exclui-se a si própria (o html: «p.jobon_id
        // !== view.jobId»).
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        await SeedTechnicalAsync("T3");
        var propria = await SeedProducaoAtivaAsync(
            machine, "T3", null, null,
            DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-3), null, "202601");

        var facts = await _query.FindAsync(new[] { "T3" }, null, propria, Hoje.AddDays(-3), null);

        Assert.Null(Assert.Single(facts).AvisoUsoParalelo);
    }

    [Fact]
    public async Task FindAsync_MesmaMaquina_NaoContaComoUsoParalelo()
    {
        // Na mesma máquina duas produções não trabalham ao mesmo tempo: uma
        // segue a outra (o Diogo, 17/10/2026: 202616 e 202618 na C2). Noutra
        // máquina com as datas a cruzar, avisa.
        var c2 = await SeedMachineAsync("C2");
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T3", ToolType.Cm, "6018", "66");
        await SeedTechnicalAsync("T3");
        await SeedProducaoAtivaAsync(c2, "T3", null, null, Hoje.AddDays(20), null, "202618");

        var mesma = await _query.FindAsync(new[] { "T3" }, "C2", null, Hoje.AddDays(-30), Hoje.AddDays(60));
        Assert.Null(Assert.Single(mesma).AvisoUsoParalelo);

        await SeedProducaoAtivaAsync(b1, "T3", null, null, Hoje, null, "202619");
        var outra = await _query.FindAsync(new[] { "T3" }, "C2", null, Hoje.AddDays(-30), Hoje.AddDays(60));
        var aviso = Assert.Single(outra).AvisoUsoParalelo;
        Assert.Contains("202619 (B1)", aviso);
        Assert.DoesNotContain("202618", aviso);
    }

    [Theory]
    [InlineData(-1, false)] // a outra acabou na véspera
    [InlineData(0, true)]   // a outra acaba no dia em que esta começa
    [InlineData(1, true)]   // a outra acaba no dia seguinte
    public async Task FindAsync_OutraAcabaNoDiaEmQueEstaComeca_Avisa_NaVesperaNao(int fimDaOutra, bool avisa)
    {
        // Os dias contam inteiros, como no aviso de sobreposição da mesma
        // máquina: no dia em que esta começa, a outra ainda tem a ferramenta.
        var b1 = await SeedMachineAsync("B1");
        await SeedMachineAsync("C2");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        var inicio = new DateOnly(2026, 4, 12);
        await SeedProducaoAtivaAsync(b1, "T3", null, null, inicio.AddDays(-10), inicio.AddDays(fimDaOutra), "202601");

        var facts = await _query.FindAsync(new[] { "T3" }, "C2", null, inicio, inicio.AddDays(8));

        AssertUsoParalelo(Assert.Single(facts), avisa, "CM 5447 / lote 3 também está associado a 202601 (B1)");
    }

    [Theory]
    [InlineData(1, false)]  // a outra começa no dia seguinte ao fim desta
    [InlineData(0, true)]   // a outra começa no dia em que esta acaba
    [InlineData(-1, true)]  // a outra começa na véspera do fim desta
    public async Task FindAsync_OutraComecaNoDiaEmQueEstaAcaba_Avisa_NoDiaSeguinteNao(int inicioDaOutra, bool avisa)
    {
        var b1 = await SeedMachineAsync("B1");
        await SeedMachineAsync("C2");
        await SeedToolAsync("T3", ToolType.Cm, "5447", "3");
        var fim = new DateOnly(2026, 4, 20);
        await SeedProducaoAtivaAsync(b1, "T3", null, null, fim.AddDays(inicioDaOutra), fim.AddDays(10), "202601");

        var facts = await _query.FindAsync(new[] { "T3" }, "C2", null, fim.AddDays(-8), fim);

        AssertUsoParalelo(Assert.Single(facts), avisa, "CM 5447 / lote 3 também está associado a 202601 (B1)");
    }

    [Theory]
    [InlineData(ToolType.Cm, "5447", "3", "CM 5447 / lote 3 também está associado a 202601 (B1)")]
    [InlineData(ToolType.Mf, "5810", "1", "MF 5810 / lote 1 também está associado a 202601 (B1)")]
    [InlineData(ToolType.Bq, "T194", "12", "BQ T194 / lote 12 também está associado a 202601 (B1)")]
    public async Task FindAsync_UsoParaleloNoutraMaquina_AvisaParaCmMfEBq(ToolType tipo, string reference, string lot, string aviso)
    {
        // O MF e a BQ também não podem trabalhar ao mesmo tempo em duas
        // máquinas: o aviso é o mesmo que o da CM.
        var b1 = await SeedMachineAsync("B1");
        await SeedMachineAsync("C2");
        await SeedToolAsync("T-9", tipo, reference, lot);
        await SeedProducaoAtivaAsync(
            b1,
            tipo == ToolType.Cm ? "T-9" : null,
            tipo == ToolType.Mf ? "T-9" : null,
            tipo == ToolType.Bq ? "T-9" : null,
            new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 15), "202601");

        var facts = await _query.FindAsync(new[] { "T-9" }, "C2", null, new DateOnly(2026, 4, 12), new DateOnly(2026, 4, 20));

        AssertUsoParalelo(Assert.Single(facts), avisa: true, aviso);
    }

    private static void AssertUsoParalelo(JobOnToolFacts fact, bool avisa, string aviso)
    {
        if (avisa)
        {
            Assert.Contains(aviso, fact.AvisoUsoParalelo);
            Assert.Equal("202601", Assert.Single(fact.UsosParalelos).ProductionNumber);
        }
        else
        {
            Assert.Null(fact.AvisoUsoParalelo);
            Assert.Empty(fact.UsosParalelos);
        }
    }
}

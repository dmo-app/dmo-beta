using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// O Histórico da ferramenta (Diogo, 22/10/2026): uma linha por Job On, com
/// a % de uso e as OUTRAS ferramentas desse Job On (a BQ pelo BqContext: uma
/// BQ trabalha com MF diferentes); a procura sem ferramenta abre vazia.
/// </summary>
public sealed class FerramentaHistoricoQueryTests
{
    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-historico-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 3, Code = "C3" }, new Machine { Id = 4, Code = "B1" });
        db.Tools.AddRange(
            new Tool { ToolId = "cm", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "cm2", Type = ToolType.Cm, Reference = "5447", Lot = "4" },
            new Tool { ToolId = "mf1", Type = ToolType.Mf, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "mf2", Type = ToolType.Mf, Reference = "5447", Lot = "2" },
            new Tool { ToolId = "bq", Type = ToolType.Bq, Reference = "5447T173", Lot = "01" },
            new Tool { ToolId = "outra", Type = ToolType.Cm, Reference = "6012", Lot = "1" });
        db.JobOns.AddRange(
            new JobOn { Id = 1, Reference = "5447", ProductionNumber = "202501", MachineId = 3, InicioEm = new DateOnly(2025, 1, 10), FimEm = new DateOnly(2025, 1, 20) },
            new JobOn { Id = 2, Reference = "5447", ProductionNumber = "202502", MachineId = 4, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 9) },
            new JobOn { Id = 3, Reference = "6012", ProductionNumber = "202503", MachineId = 3, InicioEm = new DateOnly(2025, 5, 1), FimEm = null });
        db.CmContexts.AddRange(
            new CmContext { JobOnId = 1, ToolId = "cm" },
            new CmContext { JobOnId = 2, ToolId = "cm2" },
            // Trocada a meio da 202502: também esteve nela.
            new CmContext { ReplacedJobOnId = 2, ReplacedAtUtc = DateTime.UtcNow, ToolId = "cm" },
            new CmContext { JobOnId = 3, ToolId = "outra" });
        db.MfContexts.AddRange(
            new MfContext { JobOnId = 1, ToolId = "mf1" },
            new MfContext { JobOnId = 2, ToolId = "mf2" });
        // A mesma BQ com MF diferentes, pelo bq_id de cada Job On.
        db.BqContexts.AddRange(
            new BqContext { JobOnId = 1, ToolId = "bq" },
            new BqContext { JobOnId = 2, ToolId = "bq" });
        db.FerramentaUtilizacoes.AddRange(
            Leitura(1, "cm", new DateOnly(2025, 1, 10), 10m),
            Leitura(2, "cm", new DateOnly(2025, 1, 20), 14.5m));
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static FerramentaUtilizacao Leitura(long id, string toolId, DateOnly dia, decimal percentagem) => new()
    {
        Id = id, ToolId = toolId, Percentagem = percentagem,
        RegistadaEmUtc = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(11, 0)), DateTimeKind.Utc)
    };

    private static FerramentaHistoricoQuery Query(DmoDbContext db) => new(db, new FerramentasFabricoSap(db, new Sessao()));

    [Fact]
    public async Task NoCm_UmaLinhaPorJobOn_ComOUsoEAsOutrasFerramentas()
    {
        using var db = CreateContext();

        var linhas = await Query(db).DaFerramentaAsync("cm");

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.JobOnId));
        var primeira = linhas[1];
        Assert.Equal("202501", primeira.Producao);
        Assert.Equal(4.5m, primeira.Uso);
        Assert.Equal("mf1", Assert.Single(primeira.Mf).ToolId);
        Assert.Equal("bq", Assert.Single(primeira.Bq).ToolId);
        // Na 202502 a CM foi trocada: a atual (cm2) primeiro, depois esta.
        Assert.Equal(new[] { "cm2", "cm" }, linhas[0].Cm.Select(c => c.ToolId));
        Assert.Equal("mf2", Assert.Single(linhas[0].Mf).ToolId);
        Assert.Null(linhas[0].Uso);
    }

    [Fact]
    public async Task NaBq_OsMfDeCadaJobOnPeloBqContext()
    {
        using var db = CreateContext();

        var linhas = await Query(db).DaFerramentaAsync("bq");

        Assert.Equal(new[] { "mf2", "mf1" }, linhas.Select(l => Assert.Single(l.Mf).ToolId));
        Assert.Equal(new[] { ToolType.Cm, ToolType.Mf }, HistoricoLinha.OutrosTipos(ToolType.Bq));
    }

    [Fact]
    public async Task UmaFerramentaQueNaoExiste_NaoTemHistorico()
    {
        using var db = CreateContext();

        Assert.Null(await Query(db).FerramentaAsync("nao-existe"));
        Assert.Null(await Query(db).FerramentaAsync(ToolRegistryConstants.RemovedToolId));
        Assert.Empty(await Query(db).DaFerramentaAsync("nao-existe"));
    }

    [Fact]
    public async Task SemFiltro_AProcuraFicaVazia()
    {
        using var db = CreateContext();

        var procura = await Query(db).ProcurarAsync(new HistoricoFiltro(null, null, null, null, null));

        Assert.Empty(procura.Linhas);
        Assert.False(procura.Cortada);
    }

    [Fact]
    public async Task AProcuraPelaReferenciaEPeloTipo()
    {
        using var db = CreateContext();

        var procura = await Query(db).ProcurarAsync(new HistoricoFiltro("5447", ToolType.Mf, null, null, null));

        Assert.Equal(new[] { "mf2", "mf1" }, procura.Linhas.Select(l => l.Ferramenta.ToolId));
    }

    [Fact]
    public async Task AProcuraPelaLinhaEPelosDias_SemReferencia()
    {
        using var db = CreateContext();

        var b1 = await Query(db).ProcurarAsync(new HistoricoFiltro(null, ToolType.Cm, "b1", null, null));
        var maio = await Query(db).ProcurarAsync(new HistoricoFiltro(null, null, null, new DateOnly(2025, 4, 1), null));

        Assert.All(b1.Linhas, l => Assert.Equal(2, l.JobOnId));
        Assert.Equal(new[] { "cm", "cm2" }, b1.Linhas.Select(l => l.Ferramenta.ToolId).Order());
        Assert.Equal("outra", Assert.Single(maio.Linhas).Ferramenta.ToolId);
    }

    // Regra dos dias: o Job On entra quando cruza o intervalo, limites incluídos
    // (fim >= «De» e início <= «Até»). O JobOn 2 vai de 01/03 a 09/03/2025.
    [Theory]
    [InlineData("2025-03-09", null, true)]
    [InlineData("2025-03-10", null, false)]
    [InlineData(null, "2025-03-01", true)]
    [InlineData(null, "2025-02-28", false)]
    public async Task OsDias_IncluemOsLimites_DoInicioEDoFim(string? de, string? ate, bool entra)
    {
        using var db = CreateContext();
        var filtro = new HistoricoFiltro("5447", ToolType.Mf, null,
            de is null ? null : DateOnly.Parse(de), ate is null ? null : DateOnly.Parse(ate));

        var procura = await Query(db).ProcurarAsync(filtro);
        // Sem referência passa pelos Job On da base (a outra metade da regra).
        var semReferencia = await Query(db).ProcurarAsync(filtro with { Referencia = null });

        Assert.Equal(entra, procura.Linhas.Any(l => l.JobOnId == 2));
        Assert.Equal(entra, semReferencia.Linhas.Any(l => l.JobOnId == 2));
    }

    [Fact]
    public async Task AcimaDoLimite_AProcuraFicaCortada()
    {
        using var db = CreateContext();
        db.Tools.AddRange(Enumerable.Range(1, FerramentaHistoricoQuery.MaxFerramentas + 1)
            .Select(i => new Tool { ToolId = $"muitas{i:000}", Type = ToolType.Cm, Reference = "9999", Lot = i.ToString() }));
        db.SaveChanges();

        var acima = await Query(db).ProcurarAsync(new HistoricoFiltro("9999", null, null, null, null));
        var dentro = await Query(db).ProcurarAsync(new HistoricoFiltro("5447", null, null, null, null));

        Assert.True(acima.Cortada);
        Assert.False(dentro.Cortada);
    }

    [Fact]
    public async Task ABq_MostraSoASuaParteDaReferencia()
    {
        using var db = CreateContext();

        var doCm = await Query(db).DaFerramentaAsync("cm");
        var daBq = await Query(db).FerramentaAsync("bq");
        var procura = await Query(db).ProcurarAsync(new HistoricoFiltro("T173", ToolType.Bq, null, null, null));

        Assert.All(doCm, l => Assert.Equal("T173", Assert.Single(l.Bq).Referencia));
        Assert.Equal("T173", daBq!.Referencia);
        Assert.All(procura.Linhas, l => Assert.Equal("T173", l.Ferramenta.Referencia));
        Assert.NotEmpty(procura.Linhas);
        // Sem a parte da letra (só algarismos), fica a referência como está.
        Assert.Equal("5447", FerramentaHistoricoQuery.ParteDaBq(new HistoricoFerramenta("x", ToolType.Bq, "5447", "1")).Referencia);
        Assert.Equal("5447T173", FerramentaHistoricoQuery.ParteDaBq(new HistoricoFerramenta("x", ToolType.Mf, "5447T173", "1")).Referencia);
    }

    // Dias = fim − início + 1: o dia do início conta.
    [Theory]
    [InlineData("2026-01-12", "2026-01-26", "2026-10-22", 15)]
    [InlineData("2026-03-01", "2026-03-01", "2026-10-22", 1)]
    public void OsDias_ContamODiaDoInicio(string inicio, string fim, string hoje, int dias)
    {
        Assert.Equal(dias, HistoricoLinha.Dias(DateOnly.Parse(inicio), DateOnly.Parse(fim), DateOnly.Parse(hoje)));
    }

    // Ainda na máquina (sem fim): até hoje, o dia de hoje incluído.
    [Fact]
    public void OsDias_SemFim_ContamAteHoje()
    {
        Assert.Equal(3, HistoricoLinha.Dias(new DateOnly(2026, 10, 20), null, new DateOnly(2026, 10, 22)));
        Assert.Null(HistoricoLinha.Dias(null, null, new DateOnly(2026, 10, 22)));
        Assert.Null(HistoricoLinha.Dias(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22)));
    }

    [Fact]
    public async Task CadaFerramenta_TemASuaPercentagemDeUso_NaProducao()
    {
        using var db = CreateContext();
        // O MF 1 e a BQ também com leituras antes e depois da 202501 (10/01 a 20/01).
        db.FerramentaUtilizacoes.AddRange(
            Leitura(3, "mf1", new DateOnly(2025, 1, 9), 40m),
            Leitura(4, "mf1", new DateOnly(2025, 1, 21), 47m),
            Leitura(5, "bq", new DateOnly(2025, 1, 10), 2m),
            Leitura(6, "bq", new DateOnly(2025, 1, 20), 3.25m));
        db.SaveChanges();

        var linha = (await Query(db).DaFerramentaAsync("cm")).Single(l => l.JobOnId == 1);
        var outra = (await Query(db).DaFerramentaAsync("cm")).Single(l => l.JobOnId == 2);

        Assert.Equal(4.5m, linha.UsoDe("cm"));
        Assert.Equal(7m, linha.UsoDe("mf1"));
        Assert.Equal(1.25m, linha.UsoDe("bq"));
        // Sem leituras nessa produção: null («—» na página).
        Assert.Null(outra.UsoDe("bq"));
        Assert.Null(outra.UsoDe("mf2"));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}

using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Armazem;

/// <summary>
/// Os próximos Job On no Armazém (0.2.0-beta.13): as produções que começam
/// entre hoje e o fim do período (os dois dias inclusive), pela data, com a
/// CM, o MF e a BQ em uso e o que falta: no armazém, tirar; a reparar,
/// emprestada ou na Sucata, não disponível; no Fabrico, pronta. Uma CM já
/// trocada no Job On não conta.
/// </summary>
public sealed class ArmazemProximosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 21);

    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-armazem-proximos-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Machines.AddRange(new Machine { Id = 1, Code = "B1" }, new Machine { Id = 2, Code = "C3" });
        db.Tools.AddRange(
            new Tool { ToolId = "cm", Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "mf", Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "bq", Type = ToolType.Bq, Reference = "T173", Lot = "01" },
            new Tool { ToolId = "cm-velha", Type = ToolType.Cm, Reference = "5447T173", Lot = "1" },
            new Tool { ToolId = "cm2", Type = ToolType.Cm, Reference = "6012A118", Lot = "2" });
        db.JobOns.AddRange(
            JobOn(1, 1, "5447T173", Hoje.AddDays(2)),
            JobOn(2, 2, "6012A118", Hoje),
            JobOn(3, 1, "1075C142", Hoje.AddDays(-1)),
            JobOn(4, 1, "6034B120", Hoje.AddDays(15)),
            JobOn(5, 2, "4410A077", null));
        db.CmContexts.AddRange(
            new CmContext { Id = 1, JobOnId = 1, ToolId = "cm" },
            new CmContext { Id = 2, ReplacedJobOnId = 1, ToolId = "cm-velha" },
            new CmContext { Id = 3, JobOnId = 2, ToolId = "cm2" });
        db.MfContexts.Add(new MfContext { Id = 1, JobOnId = 1, ToolId = "mf" });
        db.BqContexts.Add(new BqContext { Id = 1, JobOnId = 1, ToolId = "bq" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Id = 1, Codigo = "0042", CriadaEmUtc = DateTime.UtcNow });
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = "cm", PosicaoId = 1, MovimentoId = 1, DesdeUtc = DateTime.UtcNow });
        db.FerramentaLocalizacoes.Add(new FerramentaLocalizacao
        {
            ToolId = "mf", Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Moldes Lda", DesdeUtc = DateTime.UtcNow, MovimentoId = 2
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static JobOn JobOn(int id, int maquina, string referencia, DateOnly? inicio) =>
        new() { Id = id, MachineId = maquina, Reference = referencia, ProductionNumber = $"P{id}", InicioEm = inicio };

    [Fact]
    public async Task AsProducoesDoPeriodo_PelaDataDeInicio()
    {
        using var db = CreateContext();

        var lista = await new ArmazemProximos(db).ListarAsync(Hoje, 14);

        // Começou ontem, começa daqui a 15 dias ou sem data: fora.
        Assert.Equal([(2, "C3", "6012A118", "P2"), (1, "B1", "5447T173", "P1")],
            lista.Select(j => (j.JobOnId, j.Maquina, j.Referencia, j.Producao)).ToArray());
        Assert.Equal(4, Assert.Single(await new ArmazemProximos(db).ListarAsync(Hoje.AddDays(1), 14), j => j.JobOnId == 4).JobOnId);
        Assert.Single(await new ArmazemProximos(db).ListarAsync(Hoje, 0));
    }

    [Fact]
    public async Task CadaFerramenta_OndeEstaEOQueFalta()
    {
        using var db = CreateContext();

        var producao = (await new ArmazemProximos(db).ListarAsync(Hoje, 7)).Single(j => j.JobOnId == 1);

        Assert.Equal(
            [
                ("cm", "Armazém · posição 0042", ArmazemProximoAcao.Tirar),
                ("mf", "A reparar · Moldes Lda", ArmazemProximoAcao.Indisponivel),
                ("bq", "Fabrico", ArmazemProximoAcao.Pronta)
            ],
            producao.Ferramentas.Select(f => (f.ToolId, f.OndeEsta, f.Acao)).ToArray());
        Assert.Equal((1, 1), (producao.PorTirar, producao.Indisponiveis));
    }

    [Fact]
    public async Task SemProducoes_ListaVazia()
    {
        using var db = CreateContext();

        Assert.Empty(await new ArmazemProximos(db).ListarAsync(Hoje.AddDays(100), 7));
    }
}

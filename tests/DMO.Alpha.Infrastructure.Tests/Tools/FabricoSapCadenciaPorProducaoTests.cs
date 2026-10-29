using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A cadência por produção, como o peso (Diogo, 28/10/2026: «uma versão como
/// a do peso, atribuir sempre a produção anterior, usar uma média e moda, mas
/// mostrar o que foi usado em cada produção»).
/// </summary>
public sealed class FabricoSapCadenciaPorProducaoTests
{
    private static SapFabrico Ordem(int dia, int ateDia, int? cadencia) =>
        new() { Material = "X", Linha = "C3", InicioEm = new DateOnly(2026, 7, dia), FimEm = new DateOnly(2026, 7, ateDia), Cadencia = cadencia };

    [Fact]
    public void AMediaPesaOsDias_EAModaEADasOrdensComMaisDias()
    {
        var inicio = new DateOnly(2026, 7, 1);

        // 489 tem 4 + 2 dias, 500 tem 5: média (489 × 6 + 500 × 5) / 11.
        var resumo = FabricoSapCadencia.Resumo([Ordem(1, 5, 489), Ordem(5, 10, 500), Ordem(10, 12, 489), Ordem(12, 30, null)], inicio, new DateOnly(2026, 7, 31));

        Assert.Equal(new CadenciaSapResumo(494m, 489, 11, 3), resumo);
        Assert.Null(FabricoSapCadencia.Resumo([Ordem(1, 5, null)], inicio, null));
    }

    [Fact]
    public async Task OJobOnSemCadencia_FicaComADaProducaoAnteriorDaMesmaReferencia()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-cadencia-anterior-" + Guid.NewGuid().ToString("N"))
            .Options);
        var maquina = new Machine { Code = "C3" };
        var antiga = new JobOn { Reference = "9122Y100", ProductionNumber = "202601", Machine = maquina, InicioEm = new DateOnly(2026, 5, 1), FimEm = new DateOnly(2026, 5, 10) };
        var anterior = new JobOn { Reference = "9122Y100", ProductionNumber = "202602", Machine = maquina, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 6) };
        var nova = new JobOn { Reference = "9122Y100", ProductionNumber = "202603", Machine = maquina, InicioEm = new DateOnly(2026, 9, 1) };
        var outra = new JobOn { Reference = "5538T324", ProductionNumber = "202604", Machine = maquina, InicioEm = new DateOnly(2026, 9, 2) };
        db.JobOns.AddRange(antiga, anterior, nova, outra);
        db.SapFabricos.AddRange(
            new SapFabrico { Material = "9122Y100BRZ", Linha = "C3", InicioEm = new DateOnly(2026, 5, 1), FimEm = new DateOnly(2026, 5, 10), Cadencia = 470, LidoEmUtc = DateTime.UtcNow },
            new SapFabrico { Material = "9122Y100BRZ", Linha = "C3", InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 6), Cadencia = 489, LidoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var servico = new FerramentasFabricoSap(db, new Sessao(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance);

        // A própria produção, depois de feita.
        Assert.Equal(new CadenciaDoJobOn(new CadenciaSapResumo(489m, 489, 6, 1)), await servico.CadenciaDoJobOnAsync(anterior.Id));
        // Ainda sem SAP: a da produção anterior mais recente, com o número dela.
        var daAnterior = await servico.CadenciaDoJobOnAsync(nova.Id);
        Assert.NotNull(daAnterior);
        Assert.True(daAnterior.DaAnterior);
        Assert.Equal(489, daAnterior.Resumo.Moda);
        Assert.Equal("202602", daAnterior.AnteriorProducao);
        // Sem nenhuma produção da referência com cadência: nenhuma.
        Assert.Null(await servico.CadenciaDoJobOnAsync(outra.Id));
    }

    [Fact]
    public async Task CadaProducaoDaFerramenta_TemASuaCadencia()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-cadencia-producoes-" + Guid.NewGuid().ToString("N"))
            .Options);
        var maquina = new Machine { Code = "C3" };
        var com = new JobOn { Reference = "9122Y100", ProductionNumber = "202602", Machine = maquina, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 10) };
        var sem = new JobOn { Reference = "9122Y100", ProductionNumber = "202603", Machine = maquina, InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 5) };
        db.JobOns.AddRange(com, sem);
        db.CmContexts.AddRange(new CmContext { JobOn = com, ToolId = "CM-1" }, new CmContext { JobOn = sem, ToolId = "CM-1" });
        db.SapFabricos.AddRange(
            new SapFabrico { Material = "9122Y100BRZ", Linha = "C3", InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 7), ProducaoBruta = 656_341, Cadencia = 489, LidoEmUtc = DateTime.UtcNow },
            new SapFabrico { Material = "9122Y100BRZ", Linha = "C3", InicioEm = new DateOnly(2026, 7, 7), FimEm = new DateOnly(2026, 7, 10), ProducaoBruta = 120_000, Cadencia = 500, LidoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var producoes = (await new FerramentasFabricoSap(db, new Sessao(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance).ProducoesAsync("CM-1", 1, 10)).Pagina.Items;

        // 489 × 7 dias e 500 × 3: média 492.3, moda 489. Sem SAP, nenhuma.
        Assert.Equal(new CadenciaSapResumo(492.3m, 489, 10, 2), producoes.Single(p => p.JobOnId == com.Id).Cadencia);
        Assert.Null(producoes.Single(p => p.JobOnId == sem.Id).Cadencia);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}

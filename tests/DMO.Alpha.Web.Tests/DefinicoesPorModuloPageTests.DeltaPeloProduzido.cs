using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O delta pelo produzido (Diogo, 27/10/2026: «ver porque não mostra o
/// delta do produzido»): com uma só leitura (a manual de hoje), não há a
/// leitura antes de cada produção e o delta ficava «—» em todas; agora é a
/// parte do % de hoje que cabe a cada produção pela produção bruta, com «≈».
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task SemLeiturasAVoltaDaProducao_ODeltaEstimaSePeloProduzido()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var seguinte = new JobOn
            {
                Reference = referencia, ProductionNumber = "202502", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10)
            };
            db.JobOns.Add(seguinte);
            db.CmContexts.Add(new CmContext { JobOn = seguinte, ToolId = toolId });
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10), ProducaoBruta = 3_000_000, LidoEmUtc = DateTime.UtcNow });
            // Só a leitura manual de hoje: 16 %.
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 16m, Origem = "manual", RegistadaEmUtc = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7191", DmoCapabilities.Ferramentas);
        var quadro = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync(), "ferrProducoes"));

        // 16 % × 1/4 e 16 % × 3/4; o total continua a ser o % de hoje.
        Assert.Contains("≈ +4 %", quadro, StringComparison.Ordinal);
        Assert.Contains("≈ +12 %", quadro, StringComparison.Ordinal);
        Assert.Contains("16 %", WebUtility.HtmlDecode(ZonaUsoSap(quadro, "ferrProducoesTotal")), StringComparison.Ordinal);
        // A explicação do «≈» fica só na ajuda do título, sem o quadro verde (Diogo, 29/10/2026).
        Assert.DoesNotContain("ferrProducoesEstimado", quadro, StringComparison.Ordinal);
        Assert.Contains("é a parte do % de hoje pelo que ela produziu", ZonaUsoSap(quadro, "ferr-producoes-title"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComAsLeiturasAVoltaDaProducao_ODeltaNaoEEstimado()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.SapFabricos.Add(new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, LidoEmUtc = DateTime.UtcNow });
            db.FerramentaUtilizacoes.AddRange(
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 40m, RegistadaEmUtc = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc) },
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 47.5m, RegistadaEmUtc = new DateTime(2025, 2, 6, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7192", DmoCapabilities.Ferramentas);
        var quadro = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync(), "ferrProducoes"));

        Assert.Contains("+7.5 %", quadro, StringComparison.Ordinal);
        Assert.DoesNotContain("≈", quadro, StringComparison.Ordinal);
        Assert.DoesNotContain("ferrProducoesEstimado", quadro, StringComparison.Ordinal);
    }
}

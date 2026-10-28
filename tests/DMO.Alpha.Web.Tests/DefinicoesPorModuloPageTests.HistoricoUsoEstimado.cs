using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O % uso do Histórico de produções sem as leituras antes e depois (Diogo,
/// 28/10/2026: «as nossas consultas ao SAP não estão a funcionar bem»): o
/// mesmo «≈» da janela da utilização, a parte do % de hoje pelo produzido.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistorico_SemLeiturasAVolta_EstimaOUsoPeloProduzido()
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
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 16m, Origem = "manual", RegistadaEmUtc = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7195", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));
        var detalhes = historico[..historico.IndexOf("</details>", StringComparison.Ordinal)];

        // 16 % × 1/4 e 16 % × 3/4, como na janela da utilização.
        Assert.Contains("<strong>≈ +4 %</strong>", detalhes, StringComparison.Ordinal);
        Assert.Contains("<strong>≈ +12 %</strong>", detalhes, StringComparison.Ordinal);
    }
}

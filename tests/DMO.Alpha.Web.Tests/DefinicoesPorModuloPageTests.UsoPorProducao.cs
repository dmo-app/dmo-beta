using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O uso no fim de cada produção (Diogo, 27/10/2026: «sempre 16% uso?»): a
/// leitura manual de hoje não é o uso no fim das produções de há meses, que já
/// deram lugar a outra; nessas o uso estima-se pelo produzido até ao fim delas.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task ALeituraDeHoje_NaoEOUsoNoFimDasProducoesAnteriores()
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
            // Só a leitura manual de hoje, depois das duas: 16 %.
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 16m, Origem = "manual", RegistadaEmUtc = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7196", DmoCapabilities.Ferramentas);
        var quadro = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}&jobon=1")).Content.ReadAsStringAsync(), "ferrProducoes"));

        // 202501: 16 % × 1/4 até ao fim dela; 202502: a leitura de hoje, já depois do fim.
        Assert.Contains("<td>≈ 4 %</td>", quadro, StringComparison.Ordinal);
        Assert.Contains("<td>16 %</td>", quadro, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>≈ 16 %</td>", quadro, StringComparison.Ordinal);
    }
}

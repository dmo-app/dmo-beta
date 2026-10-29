using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class DefinicoesPorModuloPageTests
{
    /// <summary>
    /// Garante (DMO-609, Diogo 29/10/2026: «o delta das garrafas é confuso, vamos
    /// deixar apenas o bruto, metemos quantidade de dias em frente às datas»): o
    /// Histórico de produções da ficha tem Produção, Datas, Garrafas, % uso, Peso
    /// médio e Cadência, sem o Δ garrafas; as datas levam os dias (o do início
    /// conta), igual na janela da Utilização por produção.
    /// </summary>
    [Fact]
    public async Task OHistoricoDeProducoes_SoGarrafasEmBruto_EOsDiasAFrenteDasDatas()
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

        var client = await LoginOperatorAsync("7609", DmoCapabilities.Ferramentas);
        var html = WebUtility.HtmlDecode(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync());
        var historico = ZonaUsoSap(html, "ferrHistoricoProducoes");

        Assert.Matches("<thead><tr><th>Produção</th><th>Datas</th><th>Garrafas</th><th>% uso</th><th>Peso médio</th><th[^>]*>Cadência</th></tr></thead>", historico);
        Assert.DoesNotContain("Δ garrafas", historico, StringComparison.Ordinal);
        Assert.DoesNotContain("+2 000 000", historico, StringComparison.Ordinal);
        Assert.Contains("01/03/2025 a 10/03/2025 · 10 dias", historico, StringComparison.Ordinal);
        Assert.Contains("21/01/2025 a 04/02/2025 · 15 dias", historico, StringComparison.Ordinal);
        // A janela da Utilização por produção mostra as datas da mesma maneira.
        Assert.Contains("01/03/2025 a 10/03/2025 · 10 dias", ZonaUsoSap(html, "ferrProducoes"), StringComparison.Ordinal);
    }
}

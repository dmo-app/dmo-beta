using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Histórico de produções da ficha mostra o artigo de cada produção (Diogo,
/// 28/10/2026: «produção não faz sentido»): a mesma ferramenta serve vários
/// artigos e o nº de produção repete-se entre eles. Sem leituras, a % uso
/// estima-se pelo produzido, com «≈», como na Utilização por produção.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistoricoDeProducoes_MostraOArtigoEAPercentagemEstimada()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.SapFabricos.Add(new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, LidoEmUtc = DateTime.UtcNow });
            // Só a leitura manual de hoje: 16 %.
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 16m, Origem = "manual", RegistadaEmUtc = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7197", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));

        Assert.Contains($"<small class=\"ferr-producao-maquina\">{referencia} · {maquina}</small>", historico, StringComparison.Ordinal);
        Assert.Contains("≈ +16 %", historico, StringComparison.Ordinal);
    }
}

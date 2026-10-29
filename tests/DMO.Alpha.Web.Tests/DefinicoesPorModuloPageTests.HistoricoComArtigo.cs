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
/// artigos e o nº de produção repete-se entre eles.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistoricoDeProducoes_MostraOArtigoDeCadaProducao()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.SapFabricos.Add(new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, LidoEmUtc = DateTime.UtcNow });
            // A produção que está na máquina hoje, com o fim previsto.
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            var agora = new JobOn
            {
                Reference = referencia, ProductionNumber = "209905", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = hoje.AddDays(-1), FimEm = hoje.AddDays(6)
            };
            db.JobOns.Add(agora);
            db.CmContexts.Add(new CmContext { JobOn = agora, ToolId = toolId });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7197", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));

        Assert.Contains($"<small class=\"ferr-producao-maquina\">{referencia} · {maquina}</small>", historico, StringComparison.Ordinal);
        // Sem o quadro «Referência: … (n)» por cima (Diogo, 29/10/2026: «a tabela em baixo já mostra»).
        Assert.DoesNotContain("ferrHistoricoReferencias", historico, StringComparison.Ordinal);
        // «em curso» na que está na máquina hoje, não nas que já acabaram.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(historico, " · em curso"));
        Assert.Contains("209905", historico, StringComparison.Ordinal);
    }
}

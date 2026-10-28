using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A cadência no Histórico de produções (Diogo, 28/10/2026: «inserir a
/// cadência no histórico de uso de cada ferramenta [...] uma versão como a do
/// peso [...] média e moda, mas mostrar o que foi usado em cada produção»).
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistorico_MostraACadenciaDeCadaProducao_EAJanelaComMediaModaEDelta()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var seguinte = new JobOn
            {
                Reference = referencia, ProductionNumber = "202502", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 11)
            };
            var semCadencia = new JobOn
            {
                Reference = referencia, ProductionNumber = "202503", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 4, 1), FimEm = new DateOnly(2025, 4, 10)
            };
            db.JobOns.AddRange(seguinte, semCadencia);
            db.CmContexts.AddRange(new CmContext { JobOn = seguinte, ToolId = toolId }, new CmContext { JobOn = semCadencia, ToolId = toolId });
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, Cadencia = 480, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 8), ProducaoBruta = 900_000, Cadencia = 500, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 8), FimEm = new DateOnly(2025, 3, 11), ProducaoBruta = 300_000, Cadencia = 510, LidoEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7194", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));

        // Uma coluna com a cadência usada (a moda) de cada produção; a janela fica fora do <details>.
        var detalhes = historico[..historico.IndexOf("</details>", StringComparison.Ordinal)];
        Assert.Contains(">Cadência</th>", detalhes, StringComparison.Ordinal);
        Assert.Contains(">500</button>", detalhes, StringComparison.Ordinal);
        Assert.Contains(">480</button>", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog", detalhes, StringComparison.Ordinal);

        // A tabela de todas: média pesada pelos dias, moda e Δ da moda contra a anterior.
        var tabela = Regex.Replace(ZonaUsoSap(historico, "ferrCadenciaTabela"), "\\s+", " ");
        Assert.Contains("Cadência por produção", historico, StringComparison.Ordinal);
        Assert.Matches("202502</strong></a>.*?</td> <td>503</td> <td>500</td> <td>\\+20</td>", tabela);
        Assert.Matches("202503</strong></a>.*?</td> <td>—</td> <td>—</td> <td>—</td>", tabela);

        var janela = ZonaUsoSap(historico, Regex.Match(historico, "data-dmo-dialog=\"(ferrCadencia\\d+)\">500</button>").Groups[1].Value);
        Assert.Contains("Produção <strong>202502</strong>", janela, StringComparison.Ordinal);
        Assert.Contains("Moda <strong>500</strong>", janela, StringComparison.Ordinal);
        Assert.Contains("data-ferr-cadencia-lugar", janela, StringComparison.Ordinal);
    }
}

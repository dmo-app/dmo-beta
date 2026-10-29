using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A janela do peso no Histórico de produções (Diogo, 27/10/2026: «abrir
/// slide para ver Moda, Média e delta da produção anterior comparado com as
/// outras produções da mesma forma que fazemos com produzido»).
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OPesoDoHistorico_AbreAJanelaComTodasAsProducoes_MediaModaEDeltaEmGEPercentagem()
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
            var semPeso = new JobOn
            {
                Reference = referencia, ProductionNumber = "202503", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 4, 1), FimEm = new DateOnly(2025, 4, 10)
            };
            db.JobOns.AddRange(seguinte, semPeso);
            db.CmContexts.AddRange(new CmContext { JobOn = seguinte, ToolId = toolId }, new CmContext { JobOn = semPeso, ToolId = toolId });
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, PesoMedioGramas = 80m, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10), ProducaoBruta = 1_250_000, PesoMedioGramas = 78m, LidoEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7193", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));

        // A janela fica fora do <details> (dentro, fechado, não se via).
        var detalhes = historico[..historico.IndexOf("</details>", StringComparison.Ordinal)];
        Assert.DoesNotContain("<dialog", detalhes, StringComparison.Ordinal);

        // Uma tabela com todas as produções: média, moda e Δ em g e em %;
        // a produção sem peso do SAP fica com «—».
        var tabela = ZonaUsoSap(historico, "ferrPesoTabela");
        Assert.Contains("Peso por produção", historico, StringComparison.Ordinal);
        Assert.Contains("<th>Moda</th>", tabela, StringComparison.Ordinal);
        Assert.Contains("−2 g · −2.50 %", tabela, StringComparison.Ordinal);
        Assert.Matches("202503</strong></a>.*?</td>\\s*<td>—</td>\\s*<td>—</td>\\s*<td>—</td>", Regex.Replace(tabela, "\\s+", " "));

        // Cada peso abre a sua janela, com o peso dessa produção em cima.
        var janela = ZonaUsoSap(historico, Regex.Match(historico, "data-dmo-dialog=\"(ferrPeso\\d+)\">78 g</button>").Groups[1].Value);
        Assert.Contains("Produção <strong>202502</strong>", janela, StringComparison.Ordinal);
        Assert.Contains("Moda <strong>78 g</strong>", janela, StringComparison.Ordinal);
        Assert.Contains("data-ferr-peso-lugar", janela, StringComparison.Ordinal);
    }

    // Diogo, 29/10/2026: «anterior o que? isto é produção 202603 logo tem de
    // existir anterior» (DMO-602): o Δ compara com a produção anterior que
    // tem peso, saltando as que o SAP não tem.
    [Fact]
    public async Task OPesoDoHistorico_DeltaSaltaAsProducoesAnterioresSemPeso()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var meio = new JobOn
            {
                Reference = referencia, ProductionNumber = "202501", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 2, 10), FimEm = new DateOnly(2025, 2, 20)
            };
            var recente = new JobOn
            {
                Reference = referencia, ProductionNumber = "202502", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10)
            };
            db.JobOns.AddRange(meio, recente);
            db.CmContexts.AddRange(new CmContext { JobOn = meio, ToolId = toolId }, new CmContext { JobOn = recente, ToolId = toolId });
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, PesoMedioGramas = 80m, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10), ProducaoBruta = 1_250_000, PesoMedioGramas = 78m, LidoEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7391", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));
        Assert.Contains("−2 g · −2.50 %", ZonaUsoSap(historico, "ferrPesoTabela"), StringComparison.Ordinal);
    }
}

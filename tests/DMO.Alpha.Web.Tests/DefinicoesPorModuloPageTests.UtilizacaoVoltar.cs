using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class DefinicoesPorModuloPageTests
{
    /// <summary>
    /// Garante (DMO-668, Diogo 29/10/2026: «remover os comentários verdes aqui;
    /// Voltar aparece em cima da utilização»): a janela «Utilização por produção»
    /// da ficha abre com o link «Voltar» no canto superior esquerdo, antes do
    /// título, sem o botão «Fechar» e sem os quadros verdes «Hoje …» e «≈ …».
    /// </summary>
    [Fact]
    public async Task AUtilizacaoPorProducao_TemOVoltarPorCimaESemQuadrosVerdes()
    {
        var (toolId, _, _) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 44m, Origem = "manual", RegistadaEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7668", DmoCapabilities.Ferramentas);
        var janela = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrProducoes"));

        Assert.Matches("^\\s*(@\\*.*?\\*@\\s*)?<button type=\"button\" class=\"[^\"]*ferr-producoes-voltar\"[^>]*data-dmo-dialog-close[^>]*>Voltar</button>\\s*<h3 id=\"ferr-producoes-title\"", janela.Substring(janela.IndexOf('>') + 1));
        Assert.DoesNotContain(">Fechar</button>", janela, StringComparison.Ordinal);
        Assert.DoesNotContain("ferrProducoesHoje", janela, StringComparison.Ordinal);
        Assert.DoesNotContain("ferrProducoesEstimado", janela, StringComparison.Ordinal);
        Assert.Contains("44 %", WebUtility.HtmlDecode(ZonaUsoSap(janela, "ferrProducoesTotal")), StringComparison.Ordinal);
    }
}

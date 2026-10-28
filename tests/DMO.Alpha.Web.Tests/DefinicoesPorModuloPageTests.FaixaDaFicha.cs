using System.Net;
using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A faixa da ficha (Diogo, 27/10/2026): a Localização abre o Registar do
/// Armazém com a ferramenta e um «← Voltar» à ficha; o % da Utilização
/// continua a abrir a janela das produções, que leva ao Histórico de produções.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task ORegistarDoArmazem_ComUmaFerramenta_TemOVoltar()
    {
        var (toolId, _, _) = SeedJobOnComCm();
        var client = await LoginOperatorAsync("7194", DmoCapabilities.Ferramentas, DmoCapabilities.ArmazemGestao);

        var semVoltar = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={toolId}");
        Assert.Contains($"id=\"armRegVoltar\" href=\"/Armazem/Movimentos\" data-dmo-voltar=\"{toolId}\"", semVoltar, StringComparison.Ordinal);

        var ficha = $"/Ferramentas/Ficha?tool_id={toolId}";
        var comVoltar = WebUtility.HtmlDecode(await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={toolId}&voltar={Uri.EscapeDataString(ficha)}"));
        Assert.Contains($"id=\"armRegVoltar\" href=\"{ficha}\">Voltar</a>", comVoltar, StringComparison.Ordinal);

        // Um endereço de fora não serve de volta.
        var fora = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={toolId}&voltar={Uri.EscapeDataString("https://exemplo.pt/")}");
        Assert.DoesNotContain("exemplo.pt", fora, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AJanelaDaUtilizacao_LevaAoHistoricoDeProducoes()
    {
        var (toolId, _, _) = SeedJobOnComCm();
        var client = await LoginOperatorAsync("7195", DmoCapabilities.Ferramentas);

        var ficha = await client.GetStringAsync($"/Ferramentas/Ficha?tool_id={toolId}");
        Assert.Contains("id=\"ferrProducoesHistorico\" aria-controls=\"ferrHistoricoProducoes\">Ver o Histórico de produções</button>", ZonaUsoSap(ficha, "ferrProducoes"), StringComparison.Ordinal);
        Assert.Contains("id=\"ferrHistoricoProducoes\"", ficha, StringComparison.Ordinal);
    }
}

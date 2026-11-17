using System.Net;
using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A janela «Utilização» mostra a referência completa de cada produção (Diogo,
/// 28/10/2026: «em frente ao 202601 meter a referência total»).
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task AJanelaDaUtilizacao_MostraAReferenciaDeCadaProducao()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();

        var client = await LoginOperatorAsync("7188", DmoCapabilities.Ferramentas);
        var resposta = await client.GetAsync($"/Ferramentas/Utilizacao?tool_id={toolId}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var quadro = ZonaUsoSap(WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync()), "ferrProducoes");

        // R1 (Diogo, 30/10/2026): colunas Referência, Produção (pequena), Linha e N.º.
        Assert.Contains("<th>Referência</th>", quadro, StringComparison.Ordinal);
        Assert.True(quadro.IndexOf("<th>Referência</th>", StringComparison.Ordinal) < quadro.IndexOf("<th>Produção</th>", StringComparison.Ordinal));
        Assert.Contains($"<strong>{referencia}</strong></a></td>", quadro, StringComparison.Ordinal);
        Assert.Contains($"<td>{maquina}</td>", quadro, StringComparison.Ordinal);
        Assert.Contains("<td>1</td>", quadro, StringComparison.Ordinal);
    }
}

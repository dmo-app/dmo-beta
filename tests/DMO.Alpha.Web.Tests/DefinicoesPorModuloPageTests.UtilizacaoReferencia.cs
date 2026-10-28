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

        Assert.Contains($"<small class=\"ferr-producao-maquina\">{referencia} · {maquina}</small>", quadro, StringComparison.Ordinal);
    }
}

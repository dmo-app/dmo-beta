using System.Text.Json;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Diogo, 02/11/2026 (DMO-708): no Admin › SAP, o «Tudo | Desatualizadas» dava
/// «Unexpected token '&lt;'»: o browser pedia os códigos ao /Ferramentas/Definicoes,
/// que manda o ADMIN para /Admin/Users (HTML). A página do Admin responde-lhe.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OAdmin_PedeOsCodigosDoSapASuaPagina_ERecebeJson()
    {
        using var admin = await LoginAdminAsync();

        foreach (var url in new[] { "/Admin/Definicoes?handler=UsoSap", "/Admin/Definicoes?handler=UsoSap&todas=true" })
        {
            var resposta = await admin.GetAsync(url);
            Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("codigos").ValueKind);
        }

        var js = await (await admin.GetAsync("/js/ferramentas-sap.js")).Content.ReadAsStringAsync();
        Assert.Contains("const URL_SAP = (doAdmin ? PAGINA : \"/Ferramentas/Definicoes\") + \"?handler=UsoSap\";", js);
    }
}

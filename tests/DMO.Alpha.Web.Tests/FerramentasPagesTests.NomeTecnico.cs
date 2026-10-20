using System.Net;
using System.Text.Json;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // Sweep de 19/10/2026, 3.14 (F-2): o formulário pede a leitura do nome
    // técnico ao servidor, a mesma regra da ficha. O browser lia-o à sua
    // maneira («A.S.S023.S.F» dava CM «S.S0»). A resposta traz o tipo e os
    // detalhes técnicos, nunca uma referência (Diogo, 19/10/2026: «sempre o
    // nome da referência, e não do nome técnico»).
    [Theory]
    [InlineData("A.S.S023.S.F", "BQ", "blow head (marisa) · tipo BQ", "S023")]
    [InlineData("ABS023SC4713", "BQ", "neck ring · tipo BQ", "S023")]
    [InlineData("MST2064MPNNV", "CM", "blank mold (contra-molde) · tipo CM", "ST20")]
    [InlineData("M107544MFNNV", "MF", "blow/final mold · tipo MF", "1075")]
    public async Task NomeTecnico_LidoPeloServidor_DaOTipoEOsDetalhesSemReferencia(string nome, string tipo, string inicio, string referencia)
    {
        SeedUser("4472", "Ferramentas Nome Técnico", "provider-ferr-nome", "FerrNome1!");
        using var client = await LoginClientAsync("4472", "FerrNome1!");

        var response = await client.GetAsync($"{IndexPath}?handler=NomeTecnico&nome={Uri.EscapeDataString(nome)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(tipo, json.RootElement.GetProperty("tipo").GetString());
        var leitura = json.RootElement.GetProperty("leitura").GetString();
        Assert.StartsWith(inicio, leitura, StringComparison.Ordinal);
        Assert.DoesNotContain(referencia, leitura, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NomeTecnico_Desconhecido_SemTipoNemLeitura_EOAnonimoNaoLe()
    {
        SeedUser("4473", "Ferramentas Nome Desconhecido", "provider-ferr-nome-x", "FerrNome2!");
        using var client = await LoginClientAsync("4473", "FerrNome2!");

        var response = await client.GetAsync($"{IndexPath}?handler=NomeTecnico&nome=QUALQUER");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("tipo").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("leitura").ValueKind);

        using var anonymous = CreateClient();
        var challenged = await anonymous.GetAsync($"{IndexPath}?handler=NomeTecnico&nome=A.S.S023.S.F");
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.Contains("/login", challenged.Headers.Location?.ToString(), StringComparison.Ordinal);
    }
}

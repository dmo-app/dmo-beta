namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Diogo, 02/11/2026 (DMO-706): «metemos o tudo dentro do atualizar SAP». O
/// «Tudo | Desatualizadas» sai do topo do cartão e fica na janela do
/// «Atualizar do SAP»; o topo fica com Atualizar do SAP, Instalar e Opções.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OTudoEDesatualizadas_FicamDentroDaJanelaDoAtualizarDoSap()
    {
        using var admin = await LoginAdminAsync();
        var sap = await (await admin.GetAsync("/Definicoes?secao=admin&sub=sap")).Content.ReadAsStringAsync();

        var cartao = sap.IndexOf("id=\"admSapPasta\"", StringComparison.Ordinal);
        var topoFim = sap.IndexOf("data-sap-admin-estado", cartao, StringComparison.Ordinal);
        var janela = sap.IndexOf("id=\"ferrSapJanela\"", cartao, StringComparison.Ordinal);
        var janelaFim = sap.IndexOf("</dialog>", janela, StringComparison.Ordinal);
        Assert.True(cartao > 0 && topoFim > cartao && janela > topoFim && janelaFim > janela);

        var topo = sap[cartao..topoFim];
        Assert.DoesNotContain("data-ferr-sap-escolha", topo);
        Assert.Contains("data-ferr-sap-atualizar", topo);

        var dentro = sap[janela..janelaFim];
        Assert.Contains("data-ferr-sap-escolha=\"todas\" aria-pressed=\"true\">Tudo</button>", dentro);
        Assert.Contains("data-ferr-sap-escolha=\"espera\" aria-pressed=\"false\">Desatualizadas</button>", dentro);
        Assert.True(dentro.IndexOf("data-ferr-sap-escolha", StringComparison.Ordinal) < dentro.IndexOf("data-ferr-sap-correr", StringComparison.Ordinal));
    }
}

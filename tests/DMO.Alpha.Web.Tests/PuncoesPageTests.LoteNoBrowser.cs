using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests;

public partial class PuncoesPageTests
{
    // Teste do agente de 19/10/2026 (T597a9e6): «abc» no Lote e «Guardar
    // identificação» mostrava a bolha do browser e não enviava, por isso a
    // mensagem da app no canto nunca aparecia. A página vai para o Chromium
    // (tests/visual/nok_agente_browser.py), que vê o envio partir; a recusa
    // do servidor está em RefusedLot_KeepsTheTypedValues_AndWritesNothing.
    [Fact]
    public async Task Identificacao_OCampoDoLoteVaiParaOBrowser()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4313", "Operador Punções", "provider-puncoes-browser", "PuncoesPass3!");
        using var client = await LoginClientAsync("4313", "PuncoesPass3!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("id=\"pu-lote\"", html);
        Assert.Contains("Guardar identificação</button>", html);
        ExportarParaOBrowser.Gravar("puncoes.html", html);
    }
}

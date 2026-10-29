namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // Guarda o HTML do Peso no painel do Resumo (DMO-502) para
    // tests/visual/cobertura_b_browser.py medir no Chromium
    // (DMO_UI_EVIDENCE_DIR; sem a variável não guarda nada).
    [Fact]
    public async Task Peso_NoPainel_TemOsNumerosEALeituraNova_ParaOBrowser()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4881", "Peso Operador", "provider-folha-painel-b", "FolhaPainelB1!");
        using var client = await LoginClientAsync("4881", "FolhaPainelB1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&painel=1");

        Assert.Contains("data-dmo-embedded=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"pesoLeituraResumo\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"peso-leitura-nova\"", html, StringComparison.Ordinal);
        Assert.Contains("Registar leitura", html, StringComparison.Ordinal);
        ExportarParaOBrowser.Gravar("cobertura-b-peso-painel.html", html);
    }
}

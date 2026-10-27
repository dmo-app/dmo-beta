using System.Net;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests;

public partial class PuncoesPageTests
{
    // Diogo, 24/10/2026: mudar «Visto» para «Por verificar» mexia as larguras
    // da tabela. A página com punções vai para o Chromium
    // (tests/visual/puncoes_tabela_browser.py), que mede as colunas antes e
    // depois de trocar o estado.
    [Fact]
    public async Task Tabela_TemLargurasFixas_EVaiParaOBrowser()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4314", "Operador Punções", "provider-puncoes-tabela", "PuncoesPass4!");
        using var client = await LoginClientAsync("4314", "PuncoesPass4!");
        foreach (var numero in new[] { "41", "42" })
        {
            var added = await PostAsync(client, controloId, "Add", ("numero", numero), ("referencia", "1075"), ("lote", "1"));
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        }

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        // Sem botões nas linhas (Diogo, 27/10/2026): a tabela é uma lista que se escolhe.
        Assert.Contains("<table class=\"dmo-table pu-table pu-table-fixa\" id=\"puLista\" data-dmo-list=\"Lista de punções\">", html);
        Assert.Contains("<colgroup>", html);
        ExportarParaOBrowser.Gravar("puncoes-tabela.html", html);
    }
}

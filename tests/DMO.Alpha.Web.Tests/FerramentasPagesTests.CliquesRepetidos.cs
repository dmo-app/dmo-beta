namespace DMO.Alpha.Web.Tests;

// Cliques repetidos não mostram mensagens (Diogo, 18/10/2026): o servidor não
// cria nada repetido e a resposta a um clique repetido mostra o mesmo que a
// primeira. Nas Ferramentas, «Registar ferramenta», «Duplicar» e «Eliminar»
// eram POST nativos: com a rede lenta, cada clique chegava ao servidor e o
// ecrã mostrava a resposta do último, que encontrava o trabalho do primeiro
// já feito. Passam a gravar pela fila da página (data-dmo-parcial): um envio
// igual a outro que ainda não respondeu não sai (deep sweep, 18/10/2026).
public partial class FerramentasPagesTests
{
    [Fact]
    public async Task Registar_GravaPelaFilaDaPagina_AsMensagensNumaZona()
    {
        // B-ferramentas-2: dois cliques em «Registar ferramenta» mostravam
        // «Esta ferramenta já estava registada…» na ficha da ferramenta que o
        // próprio operador acabara de criar.
        SeedUser("4461", "Ferramentas Cliques", "provider-ferr-cliques-1", "FerrCliques1!");
        using var client = await LoginClientAsync("4461", "FerrCliques1!");

        var html = (await GetPageHtmlAsync(client, $"{IndexPath}?criar=1")).Replace("\r\n", "\n");
        Assert.Contains("<main data-dmo-troca class=\"ferr-page dmo-workspace\"", html);
        Assert.Matches("<form id=\"ferr-create-form\"[^>]*\\sdata-dmo-parcial[\\s>]", html);
        Assert.Contains("<button type=\"submit\" form=\"ferr-create-form\" class=\"dmo-button\">Registar</button>", html);
        Assert.Contains("<div id=\"ferrAvisos\" class=\"dmo-zona\" data-dmo-regiao>", html);

        // A recusa vem na zona: a mesma recusa duas vezes seguidas (a mesma
        // página) mostra-se outra vez, em vez de desaparecer do canto.
        var recusa = (await GetPageHtmlAsync(client, $"{IndexPath}?criar=1&reference=9931&bq_quantity=-1&create_refusal=InvalidLotQuantity")).Replace("\r\n", "\n");
        Assert.Matches("<div id=\"ferrAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div data-dmo-toast=\"erro\" class=\"ferr-aviso\" role=\"alert\">Use uma quantidade inteira", recusa);
        var eliminada = (await GetPageHtmlAsync(client, $"{IndexPath}?deleted=1")).Replace("\r\n", "\n");
        Assert.Matches("<div id=\"ferrAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div data-dmo-toast=\"ok\" class=\"ferr-nota\" role=\"status\">Ferramenta eliminada.", eliminada);
    }

    [Fact]
    public async Task Duplicar_GravaPelaFilaDaPagina_AJanelaNumaZona()
    {
        // B-ferramentas-3: dois cliques em «Duplicar» mostravam «O lote «7» já
        // estava registado…» na ferramenta que o próprio operador acabara de
        // criar (desde o #199 o servidor já não cria a segunda).
        SeedUser("4462", "Ferramentas Cliques", "provider-ferr-cliques-2", "FerrCliques2!");
        using var client = await LoginClientAsync("4462", "FerrCliques2!");
        var toolId = await SeedRealToolAsync();

        var html = (await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}")).Replace("\r\n", "\n");
        // A janela numa zona sem caixa. É uma janela que pode abrir com a
        // página (data-dmo-dialog-aberta): a resposta que fica nesta ficha
        // (o lote novo é o desta ferramenta) abre a página inteira e a janela
        // não fica aberta por cima.
        Assert.Matches("<div id=\"ferrDuplicar\" class=\"dmo-zona\" data-dmo-regiao>\\s*<dialog id=\"ferr-duplicate-dialog\" class=\"dmo-dialog\" aria-labelledby=\"ferr-duplicate-title\" data-dmo-dialog-aberta=\"\">\\s*<form method=\"post\" data-dmo-parcial action=\"/Ferramentas/Ficha\\?handler=Duplicate\">", html);
        // A ficha de outra ferramenta muda fora das zonas (a pergunta de
        // eliminar tem o nome dela): abre-se inteira.
        var zonaAvisos = html.IndexOf("<div id=\"ferrAvisos\"", StringComparison.Ordinal);
        Assert.True(html.IndexOf("Quer mesmo eliminar CM · 5447 · lote 3?", StringComparison.Ordinal) is var pergunta && pergunta > 0 && pergunta < zonaAvisos);
        Assert.Matches("</dialog>\\s*</div>\\s*<dialog id=\"ferr-delete-dialog\"", html);

        // Uma recusa abre a janela com a página (a página inteira, como antes).
        var recusa = (await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&duplicate_refusal=MissingNewLot")).Replace("\r\n", "\n");
        Assert.Contains("<dialog id=\"ferr-duplicate-dialog\" class=\"dmo-dialog\" aria-labelledby=\"ferr-duplicate-title\" data-dmo-dialog-aberta=\"1\">", recusa);
    }

    [Fact]
    public async Task Eliminar_GravaPelaFilaDaPagina()
    {
        // B-ferramentas-4: dois cliques em «Eliminar» eliminavam a ferramenta,
        // mas o ecrã ficava com «A ferramenta indicada não existe no registo.
        // Nenhuma eliminação foi feita.» (a resposta do segundo pedido).
        SeedUser("4463", "Ferramentas Cliques", "provider-ferr-cliques-3", "FerrCliques3!");
        using var client = await LoginClientAsync("4463", "FerrCliques3!");
        var toolId = await SeedRealToolAsync();

        var html = (await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}")).Replace("\r\n", "\n");
        Assert.Matches("<dialog id=\"ferr-delete-dialog\"[^>]*>\\s*<h3 id=\"ferr-delete-title\">Eliminar ferramenta</h3>\\s*<p class=\"dmo-aviso-discreto\">Quer mesmo eliminar CM · 5447 · lote 3\\?</p>\\s*<form method=\"post\" data-dmo-parcial action=\"/Ferramentas/Ficha\\?handler=Delete\">", html);
        // A eliminação volta à lista (outra página): o componente abre-a
        // inteira, como antes (o redirect: DeleteFlow_WarningShowsImpact_…).
    }
}

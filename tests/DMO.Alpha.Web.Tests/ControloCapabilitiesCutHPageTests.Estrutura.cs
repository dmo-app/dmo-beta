using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using DMO.Alpha.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // As duas estruturas fixas (Diogo, 27/10/2026: «criar uma estrutura para usar
    // sempre nas páginas deslizantes e outra nas páginas normais»). O painel de
    // apoio usa a do painel: título, «Abrir página» e «Fechar» no canto, o aviso
    // das alterações por guardar e o corpo com a página.
    [Fact]
    public async Task Estrutura_PainelDeApoio_TemOEsqueletoFixo()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedOperacional("4720", "provider-oper-estrutura", "OperEstrutura1!");
        using var client = await LoginClientAsync("4720", "OperEstrutura1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");

        Assert.Contains(
            "<dialog class=\"dmo-painel dmo-support\" data-dmo-painel-proprio id=\"dmoSupport\" data-dmo-estrutura=\"painel\" data-lado=\"direita\" data-largura=\"larga\" aria-labelledby=\"dmoSupportTitulo\">"
            + "<header class=\"dmo-painel-topo\"><h2 class=\"dmo-painel-titulo\" id=\"dmoSupportTitulo\">Detalhe</h2>"
            + "<div class=\"dmo-painel-acoes\"><a id=\"dmoSupportFull\" class=\"dmo-button\" target=\"_blank\">Abrir página</a>"
            + "<button type=\"button\" class=\"dmo-button\" data-dmo-painel-fechar>Fechar</button></div></header>"
            + "<div class=\"dmo-painel-aviso\" hidden>", html);
        Assert.Matches(new Regex("<div class=\"dmo-painel-corpo\">\\s*<p class=\"dmo-support-loading\" role=\"status\">A carregar…</p>\\s*<iframe title=\"Página de apoio\" class=\"dmo-support-frame\"></iframe>\\s*</div></dialog>"), html);
        Assert.Contains("/css/dmo-estrutura.css", html);
        Assert.True(html.IndexOf("/js/dmo-painel.js", StringComparison.Ordinal) < html.IndexOf("/js/dmo-support.js", StringComparison.Ordinal));
    }

    // A estrutura da página: o <main> fica com os atributos da página tal como
    // estão; por dentro, o topo (← Voltar, título, o que fica ao lado, ações no
    // canto) e o corpo que desliza.
    [Fact]
    public async Task Estrutura_Pagina_TopoComTituloEAcoesECorpo()
    {
        var atributos = new TagHelperAttributeList { { "class", "x-page dmo-workspace" }, { new TagHelperAttribute("data-dmo-troca") } };
        var contexto = new TagHelperContext("dmo-pagina", atributos, new Dictionary<object, object>(), "p");
        async Task Filho(TagHelper filho, string nome, string html)
        {
            var c = new TagHelperContext(nome, [], contexto.Items, nome);
            var o = new TagHelperOutput(nome, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().AppendHtml(html)));
            await filho.ProcessAsync(c, o);
            Assert.True(o.IsContentModified || o.TagName is null);
        }
        var saida = new TagHelperOutput("dmo-pagina", new TagHelperAttributeList(atributos), async (_, _) =>
        {
            await Filho(new DmoVoltarTagHelper(), "dmo-voltar", "<a href=\"/v\">← Voltar</a>");
            await Filho(new DmoTopoTagHelper(), "dmo-topo", "<input name=\"q\">");
            await Filho(new DmoAcoesTagHelper(), "dmo-acoes", "<a class=\"dmo-button\" href=\"/i\">Imprimir</a>");
            return new DefaultTagHelperContent().AppendHtml("<section>cartão</section>");
        });

        await new DmoPaginaTagHelper { Titulo = "Página & teste", TituloId = "x-title" }.ProcessAsync(contexto, saida);

        using var escrita = new StringWriter();
        // O codificador da app (Program.cs): letras portuguesas sem entidades.
        saida.WriteTo(escrita, HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.BasicLatin, System.Text.Unicode.UnicodeRanges.Latin1Supplement, System.Text.Unicode.UnicodeRanges.GeneralPunctuation));
        Assert.Equal(
            "<main class=\"x-page dmo-workspace\" data-dmo-troca><header class=\"dmo-pagina-topo\">"
            + "<div class=\"dmo-pagina-voltar\"><a href=\"/v\">← Voltar</a></div>"
            + "<h1 class=\"dmo-pagina-titulo\" id=\"x-title\">Página &amp; teste</h1>"
            + "<div class=\"dmo-pagina-extra\"><input name=\"q\"></div>"
            + "<div class=\"dmo-pagina-acoes\"><a class=\"dmo-button\" href=\"/i\">Imprimir</a></div></header>"
            + "<div class=\"dmo-pagina-corpo\"><section>cartão</section></div></main>", escrita.ToString());
    }

    // O esqueleto é da folha da estrutura: só o corpo desliza (a página não), e
    // o painel entra pelo lado com o cabeçalho fixo e o corpo a deslizar.
    [Fact]
    public void Estrutura_Folha_SoOCorpoDesliza()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "wwwroot", "css", "dmo-estrutura.css"));
        Assert.Contains("html body .app:has(> main > .dmo-pagina-topo){display:flex;flex-direction:column;height:100dvh;min-height:0;overflow:hidden}", css);
        Assert.Matches(new Regex("html body \\.dmo-pagina-corpo\\{flex:1 1 auto;min-height:0;overflow:auto;"), css);
        Assert.Matches(new Regex("html body \\.dmo-painel-corpo\\{flex:1 1 auto;min-height:0;overflow:auto;"), css);
        Assert.Contains("html[data-dmo-embedded=\"true\"] body :is(.dmo-pagina-titulo,.dmo-pagina-voltar){display:none}", css);
        Assert.Contains(":root[data-theme=\"dark\"]{--dmo-painel-fundo:", css);
    }

    // O Job On aberto dentro do painel (Diogo, 27/10/2026: «jobon deslizante
    // precisa de ser restruturado»): o painel diz «Job On» e não «Peso», abre com
    // a largura quase toda para a folha ter a coluna do Artigo e das Notas, e a
    // folha não repete o título nem o «← Voltar».
    [Fact]
    public void Estrutura_JobOnNoPainel_TituloLarguraEFolha()
    {
        var raiz = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "wwwroot");
        var js = File.ReadAllText(Path.Combine(raiz, "js", "dmo-support.js"));
        Assert.Contains("const outrasPaginas = new Map([['/JobOn', 'Job On'], ['/JobOn/Folha', 'Job On']]);", js);
        Assert.Contains("const largas = new Set(['/JobOn', '/JobOn/Folha']);", js);
        Assert.Contains("acertarPainel(frame.contentWindow.location.pathname, doc);", js);
        var css = File.ReadAllText(Path.Combine(raiz, "css", "dmo-estrutura.css"));
        Assert.Contains("html body dialog.dmo-painel[data-largura=\"total\"]{--dmo-painel-largura:calc(100vw - 64px)}", css);
        Assert.Contains("html:root[data-dmo-embedded=\"true\"] body .jof2.jof2.jof2.jof2.jof2 .page-head h1.design-jobon-title{display:none}", css);
        Assert.Contains("html:root[data-dmo-embedded=\"true\"] body .jof2.jof2.jof2.jof2.jof2 #jofVoltar{display:none}", css);
    }

    // O painel «É outra CM/MF» da Reparação Interna com o esqueleto dos painéis
    // deslizantes: o título e, no canto, «OK» e «Fechar» (sem o «×»), o corpo
    // com a referência e o lote, e o fundo escuro, que também fecha.
    [Fact]
    public void Estrutura_PainelOutraCmMf_TemOEsqueletoDosPaineis()
    {
        var raiz = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "Pages", "ReparacaoInterna");
        var registo = File.ReadAllText(Path.Combine(raiz, "_RiRegisto.cshtml"));
        Assert.Contains("<label class=\"ri-outra-fundo\" for=\"riOutra-@codigo\" aria-hidden=\"true\"></label>", registo);
        Assert.Contains("@:<header class=\"dmo-painel-topo\">", registo);
        Assert.Contains("<h4 class=\"dmo-painel-titulo\" id=\"riOutraTitulo-@codigo\">Outra CM/MF</h4>", registo);
        Assert.Matches(new Regex("@:<div class=\"dmo-painel-acoes\">\\s*<button type=\"submit\" class=\"dmo-button ri-outra-ok\" id=\"riOutraOk-@codigo\">OK</button>\\s*<label class=\"dmo-button ri-outra-fechar\" for=\"riOutra-@codigo\""), registo);
        Assert.Contains("@:<div class=\"dmo-painel-corpo\">", registo);
        Assert.DoesNotContain(">×</label>", registo);
        var pagina = File.ReadAllText(Path.Combine(raiz, "Index.cshtml"));
        Assert.Contains(".ri-registar:has(.ri-outra-check:checked) .ri-outra-fundo{opacity:1;visibility:visible;", pagina);
    }
}

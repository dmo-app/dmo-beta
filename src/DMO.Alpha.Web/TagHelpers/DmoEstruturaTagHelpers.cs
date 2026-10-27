using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DMO.Alpha.Web.TagHelpers;

// As duas estruturas fixas da app (Diogo, 27/10/2026: «criar uma estrutura para
// usar sempre nas páginas deslizantes e outra nas páginas normais»). Cada página
// e cada painel deslizante tem o mesmo esqueleto: cabeçalho com o título e as
// ações no canto superior direito, e um corpo que desliza por dentro, sem a
// página inteira deslizar a 1366×768. O aspeto vive em css/dmo-estrutura.css e
// o abrir/fechar dos painéis em js/dmo-painel.js. Como usar:
// docs/design/estrutura.md.

/// <summary>O que os filhos (&lt;dmo-acoes&gt;, &lt;dmo-topo&gt;, &lt;dmo-voltar&gt;) entregam à estrutura.</summary>
internal sealed class DmoEstruturaPartes
{
    public IHtmlContent? Acoes { get; set; }
    public IHtmlContent? Topo { get; set; }
    public IHtmlContent? Voltar { get; set; }

    public static DmoEstruturaPartes? Do(TagHelperContext context) =>
        context.Items.TryGetValue(typeof(DmoEstruturaPartes), out var partes) ? partes as DmoEstruturaPartes : null;
}

/// <summary>
/// Página normal: <c>&lt;dmo-pagina titulo="Peso" titulo-id="peso-title" class="peso-page"&gt;</c>.
/// Dá o <c>&lt;main&gt;</c> da página com <c>.dmo-pagina-topo</c> (← Voltar, título,
/// <c>&lt;dmo-topo&gt;</c>, ações à direita) e <c>.dmo-pagina-corpo</c> com o resto.
/// </summary>
[HtmlTargetElement("dmo-pagina")]
public sealed class DmoPaginaTagHelper : TagHelper
{
    /// <summary>O título da página (h1).</summary>
    public string Titulo { get; set; } = "";

    /// <summary>O id do h1, quando a página já tinha um (os testes e os links procuram-no).</summary>
    [HtmlAttributeName("titulo-id")]
    public string? TituloId { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var partes = new DmoEstruturaPartes();
        context.Items[typeof(DmoEstruturaPartes)] = partes;
        var corpo = await output.GetChildContentAsync();

        // O <main> fica com os atributos da página tal como estão (classe, id,
        // data-dmo-troca…): a estrutura reconhece-se pelo .dmo-pagina-topo.
        output.TagName = "main";
        output.TagMode = TagMode.StartTagAndEndTag;

        var html = output.Content;
        html.AppendHtml("<header class=\"dmo-pagina-topo\">");
        if (partes.Voltar is not null)
            html.AppendHtml("<div class=\"dmo-pagina-voltar\">").AppendHtml(partes.Voltar).AppendHtml("</div>");
        html.AppendHtml("<h1 class=\"dmo-pagina-titulo\"");
        if (!string.IsNullOrWhiteSpace(TituloId))
            html.AppendHtml(" id=\"").Append(TituloId).AppendHtml("\"");
        html.AppendHtml(">").Append(Titulo).AppendHtml("</h1>");
        if (partes.Topo is not null)
            html.AppendHtml("<div class=\"dmo-pagina-extra\">").AppendHtml(partes.Topo).AppendHtml("</div>");
        if (partes.Acoes is not null)
            html.AppendHtml("<div class=\"dmo-pagina-acoes\">").AppendHtml(partes.Acoes).AppendHtml("</div>");
        html.AppendHtml("</header>");
        html.AppendHtml("<div class=\"dmo-pagina-corpo\">").AppendHtml(corpo).AppendHtml("</div>");
    }

    internal static void PrimeiraClasse(TagHelperOutput output, string classe)
    {
        var outras = output.Attributes.TryGetAttribute("class", out var atual) ? atual.Value?.ToString() : null;
        output.Attributes.SetAttribute("class", string.IsNullOrWhiteSpace(outras) ? classe : $"{classe} {outras}");
    }
}

/// <summary>
/// Painel deslizante: <c>&lt;dmo-painel id="x" titulo="Ficha"&gt;</c>. Dá um
/// <c>&lt;dialog class="dmo-painel"&gt;</c> que entra pela direita (ou pela esquerda,
/// <c>lado="esquerda"</c>) com <c>.dmo-painel-topo</c> (título, ações e «Fechar» no
/// canto superior direito), o aviso das alterações por guardar e <c>.dmo-painel-corpo</c>.
/// Abre com um botão <c>data-dmo-painel-abrir="x"</c> ou <c>DmoPainel.abrir('x')</c>.
/// </summary>
[HtmlTargetElement("dmo-painel", Attributes = "id")]
public sealed class DmoPainelTagHelper : TagHelper
{
    public string Id { get; set; } = "";

    /// <summary>O título do painel (h2).</summary>
    public string Titulo { get; set; } = "";

    /// <summary>«direita» (por omissão) ou «esquerda».</summary>
    public string Lado { get; set; } = "direita";
    /// <summary>«estreita» (420 px), «media» (640 px, por omissão), «larga» (até 1040 px) ou «total» (o ecrã menos 64 px).</summary>
    /// <summary>«estreita» (420 px), «media» (640 px, por omissão) ou «larga» (até 1040 px).</summary>
    public string Largura { get; set; } = "media";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var partes = new DmoEstruturaPartes();
        context.Items[typeof(DmoEstruturaPartes)] = partes;
        var corpo = await output.GetChildContentAsync();
        var tituloId = Id + "Titulo";

        output.TagName = "dialog";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", Id);
        DmoPaginaTagHelper.PrimeiraClasse(output, "dmo-painel");
        output.Attributes.SetAttribute("data-dmo-estrutura", "painel");
        output.Attributes.SetAttribute("data-lado", Lado == "esquerda" ? "esquerda" : "direita");
        output.Attributes.SetAttribute("data-largura", Largura is "estreita" or "larga" or "total" ? Largura : "media");
        output.Attributes.SetAttribute("aria-labelledby", tituloId);

        var html = output.Content;
        html.AppendHtml("<header class=\"dmo-painel-topo\">");
        html.AppendHtml("<h2 class=\"dmo-painel-titulo\" id=\"").Append(tituloId).AppendHtml("\">").Append(Titulo).AppendHtml("</h2>");
        html.AppendHtml("<div class=\"dmo-painel-acoes\">");
        if (partes.Acoes is not null) html.AppendHtml(partes.Acoes);
        html.AppendHtml("<button type=\"button\" class=\"dmo-button\" data-dmo-painel-fechar>Fechar</button>");
        html.AppendHtml("</div></header>");
        html.AppendHtml("<div class=\"dmo-painel-aviso\" hidden><p>Existem alterações por guardar neste painel.</p>"
            + "<button type=\"button\" class=\"dmo-button\" data-dmo-painel-continuar>Continuar a editar</button>"
            + "<button type=\"button\" class=\"dmo-button\" data-dmo-painel-descartar>Fechar sem guardar</button></div>");
        html.AppendHtml("<div class=\"dmo-painel-corpo\">").AppendHtml(corpo).AppendHtml("</div>");
    }
}

/// <summary>As ações do cabeçalho (canto superior direito) de uma página ou de um painel.</summary>
[HtmlTargetElement("dmo-acoes", ParentTag = "dmo-pagina")]
[HtmlTargetElement("dmo-acoes", ParentTag = "dmo-painel")]
public sealed class DmoAcoesTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (DmoEstruturaPartes.Do(context) is { } partes) partes.Acoes = await output.GetChildContentAsync();
        output.SuppressOutput();
    }
}

/// <summary>O que fica no cabeçalho ao lado do título (por exemplo, a procura do Job On).</summary>
[HtmlTargetElement("dmo-topo", ParentTag = "dmo-pagina")]
public sealed class DmoTopoTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (DmoEstruturaPartes.Do(context) is { } partes) partes.Topo = await output.GetChildContentAsync();
        output.SuppressOutput();
    }
}

/// <summary>O «← Voltar» por cima do título, quando se vem de outra página.</summary>
[HtmlTargetElement("dmo-voltar", ParentTag = "dmo-pagina")]
public sealed class DmoVoltarTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (DmoEstruturaPartes.Do(context) is { } partes) partes.Voltar = await output.GetChildContentAsync();
        output.SuppressOutput();
    }
}

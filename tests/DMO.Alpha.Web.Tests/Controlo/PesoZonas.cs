using System.Text;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// A troca de zonas vista do lado do servidor (docs/ui-reference/atualizar-so-o-que-muda.md,
/// «Como validar»): o que o DmoTroca compara entre a página atual e a nova. Fora das
/// zonas (data-dmo-regiao) e dos blocos que ficam (data-dmo-fica), a página tem de ser
/// igual, sem contar o token antifalsificação; senão o componente abre a página inteira.
/// </summary>
internal static class PesoZonas
{
    private static readonly Regex AberturaDeZona = new("<div\\b[^>]*\\bdata-dmo-(?:regiao|fica)\\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Div = new("<div\\b|</div>", RegexOptions.Compiled);
    private static readonly Regex Token = new("(name=\"__RequestVerificationToken\" type=\"hidden\") value=\"[^\"]*\"", RegexOptions.Compiled);

    /// <summary>A raiz da troca (data-dmo-troca) com as zonas e os blocos que ficam vazios.</summary>
    public static string ForaDasZonas(string html)
    {
        var marca = html.IndexOf("data-dmo-troca", StringComparison.Ordinal);
        Assert.True(marca >= 0, "a página não tem data-dmo-troca");
        // A raiz é um div (a do Job On) ou fica dentro do main da página.
        var tag = html.LastIndexOf('<', marca);
        string raiz;
        if (string.CompareOrdinal(html, tag, "<div", 0, 4) == 0)
        {
            raiz = html[tag..FimDoDiv(html, html.IndexOf('>', marca) + 1)];
        }
        else
        {
            var inicio = html.LastIndexOf("<main", marca, StringComparison.Ordinal);
            raiz = html[inicio..html.IndexOf("</main>", marca, StringComparison.Ordinal)];
        }

        var fora = new StringBuilder();
        var i = 0;
        for (var m = AberturaDeZona.Match(raiz); m.Success; m = AberturaDeZona.Match(raiz, i))
        {
            var dentro = m.Index + m.Length;
            fora.Append(raiz, i, dentro - i);
            i = FimDoDiv(raiz, dentro);
        }
        fora.Append(raiz, i, raiz.Length - i);
        return Token.Replace(fora.ToString(), "$1");
    }

    /// <summary>O elemento com este id (a abertura, o conteúdo e o fecho do div).</summary>
    public static string Zona(string html, string id)
    {
        var abertura = new Regex($"<div id=\"{Regex.Escape(id)}\"[^>]*>").Match(html);
        Assert.True(abertura.Success, $"a página não tem #{id}");
        var fim = FimDoDiv(html, abertura.Index + abertura.Length);
        return html[abertura.Index..(fim + "</div>".Length)];
    }

    private static int FimDoDiv(string html, int desde)
    {
        var nivel = 1;
        for (var m = Div.Match(html, desde); m.Success; m = m.NextMatch())
        {
            nivel += m.Value == "</div>" ? -1 : 1;
            if (nivel == 0)
            {
                return m.Index;
            }
        }

        throw new InvalidOperationException("um div da página ficou por fechar");
    }
}

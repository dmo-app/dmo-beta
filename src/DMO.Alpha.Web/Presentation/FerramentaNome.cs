using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using Microsoft.AspNetCore.Html;

namespace DMO.Alpha.Web.Presentation;

/// <summary>Como se escreve o nome de uma CM, MF ou BQ nas listas e nos cartões.</summary>
public static class FerramentaNome
{
    /// <summary>
    /// O nome curto da ferramenta (Diogo, 22/10/2026): «CM 5447 · Lote 3»
    /// (os algarismos do início da referência) e, a seguir, a referência
    /// completa em texto pequeno (5447T173).
    /// </summary>
    public static string Curto(ToolType? tipo, string referencia, string lote) =>
        (tipo is { } t ? $"{ArmazemTextos.Tipo(t)} " : string.Empty) + $"{ReferenciaCurta(referencia)} · Lote {lote}";

    public static string ReferenciaCurta(string referencia)
    {
        var digitos = new string(referencia.TakeWhile(char.IsAsciiDigit).ToArray());
        return digitos.Length > 0 ? digitos : referencia;
    }

    /// <summary>«CM 5447 · Lote 3» em destaque e a referência completa, pequena, a seguir (se é diferente).</summary>
    public static IHtmlContent Html(ToolType? tipo, string referencia, string lote)
    {
        var html = new HtmlContentBuilder();
        html.AppendHtml("<strong class=\"dmo-ferr-nome\">").Append(Curto(tipo, referencia, lote)).AppendHtml("</strong>");
        if (ReferenciaCurta(referencia) != referencia)
        {
            html.AppendHtml(" <small class=\"dmo-ferr-ref\">").Append(referencia).AppendHtml("</small>");
        }

        return html;
    }
}

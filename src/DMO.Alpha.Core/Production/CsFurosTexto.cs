using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Production;

/// <summary>
/// CS · Furos (o Diogo, 23/10/2026): dois números de um dígito, abertos e
/// fechados, guardados no campo de texto que já existe (<c>cs_furos</c>) como
/// «Abertos 4 / Fechados 2». Um valor antigo (ex. «4 × 4») não se lê: fica
/// como está e mostra-se tal e qual.
/// </summary>
public static partial class CsFurosTexto
{
    [GeneratedRegex(@"^\s*(?:Abertos\s+(?<a>\d))?\s*/?\s*(?:Fechados\s+(?<f>\d))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Formato();

    /// <summary>Os abertos e os fechados; <c>Lido</c> false quando o texto não é deste formato (ou vazio).</summary>
    public static (bool Lido, string? Abertos, string? Fechados) Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (false, null, null);
        var m = Formato().Match(texto);
        if (!m.Success) return (false, null, null);
        var a = m.Groups["a"].Success ? m.Groups["a"].Value : null;
        var f = m.Groups["f"].Success ? m.Groups["f"].Value : null;
        return a is null && f is null ? (false, null, null) : (true, a, f);
    }

    public static string Escrever(string? abertos, string? fechados)
    {
        static string? Digito(string? v) => v?.Trim() is { Length: 1 } d && char.IsAsciiDigit(d[0]) ? d : null;
        var a = Digito(abertos);
        var f = Digito(fechados);
        return (a, f) switch
        {
            (null, null) => "",
            (_, null) => $"Abertos {a}",
            (null, _) => $"Fechados {f}",
            _ => $"Abertos {a} / Fechados {f}",
        };
    }
}

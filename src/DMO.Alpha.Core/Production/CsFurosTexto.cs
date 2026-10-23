namespace DMO.Alpha.Core.Production;

/// <summary>
/// CS · Furos (o Diogo, 23/10/2026): «Aberto/Fechado» e um número, guardado no
/// campo de texto que já existe (<c>cs_furos</c>) como «Fechado · 4». Um valor
/// antigo (ex. «4 × 4») não se lê: fica como está e mostra-se tal e qual.
/// </summary>
public static class CsFurosTexto
{
    public const string Aberto = "Aberto";
    public const string Fechado = "Fechado";

    public static (string? Estado, string? Numero) Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);
        var partes = texto.Split('·', 2, StringSplitOptions.TrimEntries);
        var estado = partes[0] switch
        {
            var e when e.Equals(Aberto, StringComparison.OrdinalIgnoreCase) => Aberto,
            var e when e.Equals(Fechado, StringComparison.OrdinalIgnoreCase) => Fechado,
            _ => null,
        };
        if (estado is null) return (null, null);
        var numero = partes.Length > 1 && partes[1].Length > 0 && partes[1].All(char.IsAsciiDigit) ? partes[1] : null;
        return (estado, numero);
    }

    public static string Escrever(string? estado, string? numero)
    {
        estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();
        numero = string.IsNullOrWhiteSpace(numero) ? null : numero.Trim();
        return estado is null ? numero ?? "" : numero is null ? estado : $"{estado} · {numero}";
    }
}

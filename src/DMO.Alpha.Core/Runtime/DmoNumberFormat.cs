using System.Globalization;

namespace DMO.Alpha.Core.Runtime;

/// <summary>Portuguese labels/dates with the owner's decimal-point convention.</summary>
public static class DmoNumberFormat
{
    public static CultureInfo Culture { get; } = CreateCulture();

    /// <summary>Two decimals with an explicit sign; values that round to zero show «+0.00».</summary>
    public static string Signed(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return (rounded >= 0 ? "+" : "−") + Math.Abs(rounded).ToString("0.00", Culture);
    }

    /// <summary>
    /// Duas casas, sem «.00» num número inteiro (Diogo, 23/10/2026: «100.00 g» é «100 g»);
    /// com parte decimal ficam as duas casas («100.50»).
    /// </summary>
    public static string Decimal2(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString(rounded == decimal.Truncate(rounded) ? "0" : "0.00", Culture);
    }

    /// <summary>
    /// Para os campos de editar: como <see cref="Decimal2"/> quando o valor
    /// cabe em duas casas; com mais casas, mostra-as todas (sem zeros no fim),
    /// para gravar outra vez não arredondar o que foi medido (nada se perde).
    /// </summary>
    public static string Decimal2SemPerda(decimal value) =>
        Math.Round(value, 2) == value ? Decimal2(value) : value.ToString("0.#####", Culture);

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("pt-PT").Clone();
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.NumberFormat.NumberGroupSeparator = string.Empty;
        culture.NumberFormat.PercentDecimalSeparator = ".";
        culture.NumberFormat.PercentGroupSeparator = string.Empty;
        return CultureInfo.ReadOnly(culture);
    }
}

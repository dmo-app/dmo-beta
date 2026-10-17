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

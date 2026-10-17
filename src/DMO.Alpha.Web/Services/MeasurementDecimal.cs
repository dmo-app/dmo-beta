using System.Globalization;

namespace DMO.Alpha.Web.Services;

/// <summary>Physical measurements accept a decimal comma or point, never digit grouping.</summary>
internal static class MeasurementDecimal
{
    public static decimal? Parse(string? value) =>
        decimal.TryParse(value?.Trim().Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}

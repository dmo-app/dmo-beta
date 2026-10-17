namespace DMO.Alpha.Core.Runtime;

/// <summary>Factory wall-clock time; persisted movement instants remain UTC.</summary>
public static class DmoBusinessTime
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public static DateTime Now => ToLocal(DateTime.UtcNow);

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
        utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc, Zone);

    public static DateTime ToLocal(DateTimeOffset instant) => ToLocal(instant.UtcDateTime);

    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    /// <summary>
    /// Uma hora local que cai no salto da mudança de hora (março: 01:00–01:59
    /// não existe em Portugal continental) passa para a hora seguinte, somando
    /// o salto (01:30 → 02:30). Uma hora válida volta igual. <paramref name="shifted"/>
    /// diz se houve ajuste, para a página avisar em vez de recusar.
    /// </summary>
    public static DateTime NextValidLocal(DateTime local, out bool shifted)
    {
        var candidate = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        shifted = false;
        // O salto de Lisboa é de uma hora; o limite protege contra uma zona inesperada.
        for (var attempt = 0; attempt < 4 && Zone.IsInvalidTime(candidate); attempt++)
        {
            candidate = candidate.AddHours(1);
            shifted = true;
        }

        return candidate;
    }
}

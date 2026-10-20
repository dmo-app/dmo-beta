using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Infrastructure.Tests.Runtime;

public class DmoRuntimeFormatTests
{
    [Theory]
    [InlineData("1.234", "+1.23")]
    [InlineData("-0.53", "−0.53")]
    [InlineData("-0.004", "+0.00")]
    [InlineData("-0.005", "−0.01")]
    [InlineData("0", "+0.00")]
    public void Signed_RoundsBeforeChoosingTheSign(string value, string expected)
    {
        Assert.Equal(expected, DmoNumberFormat.Signed(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ToLocal_ShowsLisbonTimeForUtcAndOffsetInstants()
    {
        var summerUtc = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var winterUtc = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(11, DmoBusinessTime.ToLocal(summerUtc).Hour);
        Assert.Equal(10, DmoBusinessTime.ToLocal(winterUtc).Hour);
        Assert.Equal(11, DmoBusinessTime.ToLocal(new DateTimeOffset(summerUtc)).Hour);
        Assert.Equal(11, DmoBusinessTime.ToLocal(summerUtc.ToLocalTime()).Hour);
    }

    [Fact]
    public void NextValidLocal_MovesATimeInTheMarchGapToTheNextHour()
    {
        // 29/03/2026: em Lisboa as 01:00 passam a 02:00; 01:30 não existe.
        var shiftedLocal = DmoBusinessTime.NextValidLocal(new DateTime(2026, 3, 29, 1, 30, 0), out var shifted);

        Assert.True(shifted);
        Assert.Equal(new DateTime(2026, 3, 29, 2, 30, 0), shiftedLocal);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), DmoBusinessTime.ToUtc(shiftedLocal));
    }

    [Theory]
    [InlineData(2026, 3, 29, 0, 59)]
    [InlineData(2026, 3, 29, 2, 0)]
    [InlineData(2026, 10, 25, 1, 30)]
    [InlineData(2026, 7, 1, 10, 0)]
    public void NextValidLocal_KeepsAValidTime(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0);

        Assert.Equal(local, DmoBusinessTime.NextValidLocal(local, out var shifted));
        Assert.False(shifted);
    }
}

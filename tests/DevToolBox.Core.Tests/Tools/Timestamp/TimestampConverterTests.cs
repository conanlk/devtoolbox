using DevToolBox.Core.Tools.Timestamp;

namespace DevToolBox.Core.Tests.Tools.Timestamp;

public class TimestampConverterTests
{
    private static readonly DateTimeOffset Expected = new(2026, 10, 15, 7, 30, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Saigon = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [Theory]
    [InlineData("1792049400", TimestampUnit.Seconds)]
    [InlineData("1792049400000", TimestampUnit.Milliseconds)]
    [InlineData("1792049400000000", TimestampUnit.Microseconds)]
    [InlineData("1792049400000000000", TimestampUnit.Nanoseconds)]
    [InlineData(" 1_792_049_400 ", TimestampUnit.Seconds)]
    public void FromEpoch_AutoDetectsUnit(string input, TimestampUnit unit)
    {
        var result = TimestampConverter.FromEpoch(input).Value;

        Assert.Equal(unit, result.Unit);
        Assert.Equal(Expected, result.Utc);
    }

    [Fact]
    public void FromEpoch_HandlesFractionsNegativesAndExplicitUnit()
    {
        Assert.Equal(Expected.AddMilliseconds(500), TimestampConverter.FromEpoch("1792049400.5").Value.Utc);
        Assert.Equal(new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero), TimestampConverter.FromEpoch("-1").Value.Utc);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMilliseconds(1000), TimestampConverter.FromEpoch("1000", TimestampUnit.Milliseconds).Value.Utc);
    }

    [Theory]
    [InlineData("", "timestamp.not-a-number")]
    [InlineData("abc", "timestamp.not-a-number")]
    [InlineData("99999999999999999999999", "timestamp.out-of-range")]
    public void FromEpoch_Invalid_IsAnError(string input, string code) =>
        Assert.Equal(code, TimestampConverter.FromEpoch(input).Error?.Code);

    [Theory]
    [InlineData("2026-10-15 14:30:00")]
    [InlineData("2026-10-15T14:30")]
    [InlineData("2026-10-15T07:30:00Z")]
    [InlineData("2026-10-15T09:30:00+02:00")]
    public void ParseDateTime_UsesZoneUnlessOffsetIsExplicit(string input) =>
        Assert.Equal(Expected, TimestampConverter.ParseDateTime(input, Saigon).Value.ToUniversalTime());

    [Fact]
    public void ParseDateTime_TimeInDstGap_IsAnError()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        Assert.Equal("timestamp.dst-gap", TimestampConverter.ParseDateTime("2026-03-08 02:30", newYork).Error?.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tomorrow-ish")]
    public void ParseDateTime_Invalid_IsAnError(string input) =>
        Assert.False(TimestampConverter.ParseDateTime(input, TimeZoneInfo.Utc).IsOk);

    [Fact]
    public void Describe_ListsAllRepresentations()
    {
        var fields = TimestampConverter.Describe(Expected, Saigon, Expected.AddHours(3)).ToDictionary(f => f.Label, f => f.Value);

        Assert.Equal("2026-10-15 14:30:00.000 +07:00", fields["Local (Asia/Ho_Chi_Minh)"]);
        Assert.Equal("2026-10-15 07:30:00.000 UTC", fields["UTC"]);
        Assert.Equal("2026-10-15T14:30:00.000+07:00", fields["ISO 8601"]);
        Assert.Equal("Thu, 15 Oct 2026 14:30:00 +0700", fields["RFC 2822"]);
        Assert.Equal("3 hours ago", fields["Relative"]);
        Assert.Equal("1792049400", fields["Unix seconds"]);
        Assert.Equal("1792049400000", fields["Unix milliseconds"]);
        Assert.Equal("1792049400000000000", fields["Unix nanoseconds"]);
        Assert.Equal("2026-W42", fields["ISO week"]);
    }

    [Fact]
    public void ToIso8601_UsesZForUtc() =>
        Assert.Equal("2026-10-15T07:30:00.000Z", TimestampConverter.ToIso8601(Expected));
}

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(-30, "30 seconds ago")]
    [InlineData(-60, "1 minute ago")]
    [InlineData(-3 * 3600, "3 hours ago")]
    [InlineData(2 * 86400, "in 2 days")]
    [InlineData(-90 * 86400, "3 months ago")]
    [InlineData(800 * 86400, "in 2 years")]
    public void Format_PicksLargestUnit(int seconds, string expected) =>
        Assert.Equal(expected, RelativeTime.Format(Now.AddSeconds(seconds), Now));
}

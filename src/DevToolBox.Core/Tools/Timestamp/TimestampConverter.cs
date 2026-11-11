using System.Globalization;
using System.Text.RegularExpressions;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Timestamp;

public enum TimestampUnit
{
    Auto,
    Seconds,
    Milliseconds,
    Microseconds,
    Nanoseconds,
}

public sealed record EpochConversion(TimestampUnit Unit, DateTimeOffset Utc);

/// <param name="Key">Stable English name, e.g. "Unix seconds" (the label is localized).</param>
public sealed record TimestampField(string Key, string Label, string Value);

/// <summary>Unix epoch ↔ date/time conversions with automatic unit detection.</summary>
public static partial class TimestampConverter
{
    private const decimal TicksPerSecond = TimeSpan.TicksPerSecond;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Parses an epoch value. Auto picks the unit from the number of integer digits.</summary>
    public static Result<EpochConversion> FromEpoch(string? input, TimestampUnit unit = TimestampUnit.Auto)
    {
        var text = (input ?? string.Empty).Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant, out var value))
        {
            return new Error("timestamp.not-a-number", CoreStrings.Timestamp_NotANumber);
        }

        var resolved = unit == TimestampUnit.Auto ? DetectUnit(value) : unit;
        var ticks = value * TicksPerUnit(resolved);
        var maxTicks = (decimal)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);
        var minTicks = (decimal)(DateTimeOffset.MinValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);
        if (ticks > maxTicks || ticks < minTicks)
        {
            return new Error("timestamp.out-of-range", CoreLocalization.Format(CoreStrings.Timestamp_OutOfRange, UnitName(resolved)));
        }

        return new EpochConversion(resolved, DateTimeOffset.UnixEpoch.AddTicks((long)decimal.Truncate(ticks)));
    }

    public static TimestampUnit DetectUnit(decimal value)
    {
        var digits = decimal.Truncate(Math.Abs(value)).ToString(Invariant).Length;
        return digits switch
        {
            <= 11 => TimestampUnit.Seconds,
            <= 14 => TimestampUnit.Milliseconds,
            <= 17 => TimestampUnit.Microseconds,
            _ => TimestampUnit.Nanoseconds,
        };
    }

    /// <summary>
    /// Parses a date/time. Text with an explicit offset ("Z", "+07:00") uses it; otherwise the time is
    /// interpreted in <paramref name="zone"/>.
    /// </summary>
    public static Result<DateTimeOffset> ParseDateTime(string? input, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new Error("timestamp.empty-date", CoreStrings.Timestamp_EmptyDate);
        }

        if (ExplicitOffset().IsMatch(text))
        {
            return DateTimeOffset.TryParse(text, Invariant, DateTimeStyles.AllowWhiteSpaces, out var withOffset)
                ? withOffset
                : InvalidDate();
        }

        if (!DateTime.TryParse(text, Invariant, DateTimeStyles.AllowWhiteSpaces, out var local))
        {
            return InvalidDate();
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            return new Error("timestamp.dst-gap", CoreLocalization.Format(CoreStrings.Timestamp_DstGap, local.ToString("yyyy-MM-dd HH:mm", Invariant), zone.Id));
        }

        // Ambiguous times (DST fall-back) resolve to the zone's standard offset.
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>All the representations shown for one instant.</summary>
    public static IReadOnlyList<TimestampField> Describe(DateTimeOffset instant, TimeZoneInfo zone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var utc = instant.ToUniversalTime();
        var inZone = TimeZoneInfo.ConvertTime(utc, zone);
        var unixTicks = (decimal)(utc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);

        return
        [
            new("Local", CoreLocalization.Format(CoreStrings.Timestamp_Field_Local, zone.Id), inZone.ToString("yyyy-MM-dd HH:mm:ss.fff", Invariant) + " " + FormatOffset(inZone.Offset, withColon: true)),
            new("UTC", "UTC", utc.ToString("yyyy-MM-dd HH:mm:ss.fff", Invariant) + " UTC"),
            new("ISO 8601", "ISO 8601", ToIso8601(inZone)),
            new("RFC 2822", "RFC 2822", inZone.ToString("ddd, dd MMM yyyy HH:mm:ss ", Invariant) + FormatOffset(inZone.Offset, withColon: false)),
            new("Relative", CoreStrings.Timestamp_Field_Relative, RelativeTime.Format(utc, now)),
            new("Unix seconds", CoreStrings.Timestamp_Field_UnixSeconds, decimal.Floor(unixTicks / TicksPerSecond).ToString(Invariant)),
            new("Unix milliseconds", CoreStrings.Timestamp_Field_UnixMilliseconds, decimal.Floor(unixTicks / TimeSpan.TicksPerMillisecond).ToString(Invariant)),
            new("Unix microseconds", CoreStrings.Timestamp_Field_UnixMicroseconds, decimal.Floor(unixTicks / TimeSpan.TicksPerMicrosecond).ToString(Invariant)),
            new("Unix nanoseconds", CoreStrings.Timestamp_Field_UnixNanoseconds, (unixTicks * 100).ToString(Invariant)),
            new("Day of week", CoreStrings.Timestamp_Field_DayOfWeek, DayName(inZone.DayOfWeek)),
            new("Day of year", CoreStrings.Timestamp_Field_DayOfYear, inZone.DayOfYear.ToString(Invariant)),
            new("ISO week", CoreStrings.Timestamp_Field_IsoWeek, $"{ISOWeek.GetYear(inZone.DateTime)}-W{ISOWeek.GetWeekOfYear(inZone.DateTime):00}"),
        ];
    }

    public static string ToIso8601(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero
            ? value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Invariant)
            : value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", Invariant);

    private static decimal TicksPerUnit(TimestampUnit unit) => unit switch
    {
        TimestampUnit.Seconds => TicksPerSecond,
        TimestampUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
        TimestampUnit.Microseconds => TimeSpan.TicksPerMicrosecond,
        TimestampUnit.Nanoseconds => 0.01m,
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };

    /// <summary>"seconds", "milliseconds"… in the current UI language.</summary>
    public static string UnitName(TimestampUnit unit) => unit switch
    {
        TimestampUnit.Seconds => CoreStrings.Timestamp_Unit_Seconds,
        TimestampUnit.Milliseconds => CoreStrings.Timestamp_Unit_Milliseconds,
        TimestampUnit.Microseconds => CoreStrings.Timestamp_Unit_Microseconds,
        TimestampUnit.Nanoseconds => CoreStrings.Timestamp_Unit_Nanoseconds,
        _ => unit.ToString().ToLowerInvariant(),
    };

    private static string DayName(DayOfWeek day) =>
        (CoreLocalization.Culture ?? CultureInfo.CurrentUICulture).DateTimeFormat.GetDayName(day);

    private static string FormatOffset(TimeSpan offset, bool withColon)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return withColon ? $"{sign}{abs:hh\\:mm}" : $"{sign}{abs:hhmm}";
    }

    private static Error InvalidDate() =>
        new("timestamp.invalid-date", CoreStrings.Timestamp_InvalidDate);

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}

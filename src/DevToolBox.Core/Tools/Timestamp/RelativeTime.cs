using DevToolBox.Core.Localization;

namespace DevToolBox.Core.Tools.Timestamp;

/// <summary>"3 hours ago" / "in 2 days" style descriptions.</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset value, DateTimeOffset now)
    {
        var delta = value - now;
        var past = delta < TimeSpan.Zero;
        var span = delta.Duration();

        if (span < TimeSpan.FromSeconds(1))
        {
            return CoreStrings.Relative_JustNow;
        }

        var (amount, one, other) = span switch
        {
            _ when span < TimeSpan.FromMinutes(1) => ((long)span.TotalSeconds, CoreStrings.Relative_Second, CoreStrings.Relative_Seconds),
            _ when span < TimeSpan.FromHours(1) => ((long)span.TotalMinutes, CoreStrings.Relative_Minute, CoreStrings.Relative_Minutes),
            _ when span < TimeSpan.FromDays(1) => ((long)span.TotalHours, CoreStrings.Relative_Hour, CoreStrings.Relative_Hours),
            _ when span < TimeSpan.FromDays(30) => ((long)span.TotalDays, CoreStrings.Relative_Day, CoreStrings.Relative_Days),
            _ when span < TimeSpan.FromDays(365) => ((long)(span.TotalDays / 30), CoreStrings.Relative_Month, CoreStrings.Relative_Months),
            _ => ((long)(span.TotalDays / 365.2425), CoreStrings.Relative_Year, CoreStrings.Relative_Years),
        };

        var text = CoreLocalization.Format(amount == 1 ? one : other, amount);
        return CoreLocalization.Format(past ? CoreStrings.Relative_Ago : CoreStrings.Relative_In, text);
    }
}

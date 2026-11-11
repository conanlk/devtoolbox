using System.Globalization;

namespace DevToolBox.Core.Localization;

/// <summary>
/// Language of the user-facing text Core produces (error messages, Smart paste labels). The app sets it
/// when the user switches language; machine-readable output always stays culture-invariant.
/// </summary>
public static class CoreLocalization
{
    /// <summary>UI culture for Core messages; null means the thread's current UI culture.</summary>
    public static CultureInfo? Culture
    {
        get => CoreStrings.Culture;
        set => CoreStrings.Culture = value;
    }

    /// <summary>Fills a localized pattern; arguments are formatted invariantly so numbers read the same in every language.</summary>
    internal static string Format(string pattern, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, pattern, args);
}

using System.Globalization;

namespace DevToolBox.App.Localization;

/// <summary>UI languages the app ships. Persisted by name in settings.</summary>
public enum AppLanguage
{
    English,
    Vietnamese,
}

public static class AppLanguages
{
    /// <summary>Resource culture for a language; English is the neutral (fallback) resources.</summary>
    public static CultureInfo Culture(this AppLanguage language) => language switch
    {
        AppLanguage.Vietnamese => CultureInfo.GetCultureInfo("vi"),
        _ => CultureInfo.GetCultureInfo("en"),
    };

    /// <summary>First-launch default: Vietnamese when the OS UI language is Vietnamese, English otherwise.</summary>
    public static AppLanguage FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TwoLetterISOLanguageName == "vi" ? AppLanguage.Vietnamese : AppLanguage.English;
    }
}

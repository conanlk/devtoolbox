using System.Globalization;
using DevToolBox.App.Platform;

namespace DevToolBox.App.Localization;

public interface ILanguageService
{
    AppLanguage Language { get; }

    /// <summary>Applies the saved choice, or the OS language on first launch.</summary>
    void ApplySavedPreference();

    /// <summary>Switches the UI language live and remembers the choice.</summary>
    void SetLanguage(AppLanguage language);
}

public sealed class LanguageService(ISettingsStore settings) : ILanguageService
{
    public AppLanguage Language => Localizer.Language;

    public void ApplySavedPreference() =>
        Localizer.SetLanguage(settings.Current.Language ?? AppLanguages.FromCulture(CultureInfo.CurrentUICulture));

    public void SetLanguage(AppLanguage language)
    {
        if (settings.Current.Language != language)
        {
            settings.Update(s => s.Language = language);
        }

        Localizer.SetLanguage(language);
    }
}

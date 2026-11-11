using DevToolBox.App.Localization;

namespace DevToolBox.App.Tests.Localization;

/// <summary>
/// The UI language is process-wide, so tests that switch it run alone (after every other test) and always
/// switch back to English, which all other tests assert against.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LanguageSwitchCollection
{
    public const string Name = "Language switch";
}

/// <summary>Switches the UI language for the duration of a test.</summary>
internal sealed class LanguageScope : IDisposable
{
    public LanguageScope(AppLanguage language) => Localizer.SetLanguage(language);

    public void Dispose() => Localizer.SetLanguage(AppLanguage.English);
}

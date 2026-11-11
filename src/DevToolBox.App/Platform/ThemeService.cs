using Avalonia;
using Avalonia.Styling;

namespace DevToolBox.App.Platform;

public interface IThemeService
{
    ThemePreference Preference { get; }

    event EventHandler? PreferenceChanged;

    void ApplySavedPreference();

    void SetPreference(ThemePreference preference);
}

public sealed class ThemeService(ISettingsStore settings) : IThemeService
{
    public ThemePreference Preference => settings.Current.Theme;

    public event EventHandler? PreferenceChanged;

    public void ApplySavedPreference() => Apply(Preference);

    public void SetPreference(ThemePreference preference)
    {
        if (preference == Preference)
        {
            return;
        }

        settings.Update(s => s.Theme = preference);
        Apply(preference);
        PreferenceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Apply(ThemePreference preference)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

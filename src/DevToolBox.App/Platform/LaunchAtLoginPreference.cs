namespace DevToolBox.App.Platform;

/// <summary>
/// The user's "Open at login" choice. Saved in settings so the app can tell "never decided" (apply the
/// default) from "turned off" (respect it), and so the OS entry can be refreshed on every start.
/// </summary>
public sealed class LaunchAtLoginPreference(ISettingsStore settings, ILaunchAtLogin launcher)
{
    public bool IsSupported => launcher.IsSupported;

    public bool IsEnabled => IsSupported && settings.Current.LaunchAtLogin == true;

    /// <summary>
    /// Brings the OS entry in line with the saved choice. With no saved choice yet, <paramref name="enableByDefault"/>
    /// decides; it is only persisted when true, so a development build never claims the choice for release builds.
    /// </summary>
    public void ApplyOnStartup(bool enableByDefault)
    {
        if (!IsSupported)
        {
            return;
        }

        if (settings.Current.LaunchAtLogin is not { } enabled)
        {
            if (!enableByDefault)
            {
                return;
            }

            enabled = true;
            settings.Update(s => s.LaunchAtLogin = true);
        }

        // Rewritten every start: the entry must point at the executable that is running now.
        launcher.SetEnabled(enabled);
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
        {
            return;
        }

        settings.Update(s => s.LaunchAtLogin = enabled);
        launcher.SetEnabled(enabled);
    }
}

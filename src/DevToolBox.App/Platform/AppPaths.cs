namespace DevToolBox.App.Platform;

/// <summary>Per-OS locations for app data, following each platform's conventions.</summary>
public static class AppPaths
{
    private const string AppFolderName = "DevToolBox";

    public static string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");

    /// <summary>
    /// Windows: %APPDATA%\DevToolBox · macOS: ~/Library/Application Support/DevToolBox ·
    /// Linux: $XDG_CONFIG_HOME/DevToolBox (default ~/.config/DevToolBox).
    /// </summary>
    public static string ConfigDirectory => HostPlatformDetector.Current switch
    {
        HostPlatform.Windows => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName),
        HostPlatform.MacOS => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", AppFolderName),
        _ => Path.Combine(XdgConfigHome(), AppFolderName),
    };

    private static string XdgConfigHome()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
    }
}

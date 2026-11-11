using System.Diagnostics;
using System.Security;
using System.Text;

namespace DevToolBox.App.Platform;

/// <summary>Registers the app with the OS so it starts (in the background) when the user logs in.</summary>
public interface ILaunchAtLogin
{
    /// <summary>False when the running executable can't be registered (e.g. started as <c>dotnet DevToolBox.dll</c>).</summary>
    bool IsSupported { get; }

    bool IsEnabled { get; }

    /// <summary>Adds or removes the login entry. Enabling rewrites it, so it follows the app if it moved.</summary>
    void SetEnabled(bool enabled);
}

public static class LaunchAtLogin
{
    private const string MacAgentLabel = "com.devtoolbox.app";

    /// <summary>CFBundleIdentifier of DevToolBox.app (ApplicationId in the .csproj).</summary>
    private const string MacBundleIdentifier = "com.devtoolbox.app";
    private const string LinuxDesktopFileName = "devtoolbox.desktop";

    /// <summary>
    /// Windows: HKCU Run registry value · macOS: ~/Library/LaunchAgents/com.devtoolbox.app.plist ·
    /// Linux: $XDG_CONFIG_HOME/autostart/devtoolbox.desktop. All per-user; no admin rights needed.
    /// </summary>
    public static ILaunchAtLogin ForCurrentPlatform()
    {
        var executable = RegistrationPath(Environment.ProcessPath, Environment.GetEnvironmentVariable("APPIMAGE"));
        if (string.IsNullOrEmpty(executable)
            || Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return UnsupportedLaunchAtLogin.Instance;
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsLaunchAtLogin(executable);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? new FileLaunchAtLogin(Path.Combine(home, "Library", "LaunchAgents", MacAgentLabel + ".plist"), MacLaunchAgent(executable))
            : new FileLaunchAtLogin(Path.Combine(XdgConfigHome(home), "autostart", LinuxDesktopFileName), LinuxAutostartEntry(executable));
    }

    /// <summary>
    /// The file the login entry starts. An AppImage runs from a temporary mount that is gone after logout, so it
    /// registers the .AppImage file itself (the runtime puts its path in $APPIMAGE).
    /// </summary>
    public static string? RegistrationPath(string? processPath, string? appImage) =>
        string.IsNullOrEmpty(appImage) ? processPath : appImage;

    /// <summary>
    /// A launchd user agent that runs once at login (RunAtLoad) and is not restarted after Quit.
    /// AssociatedBundleIdentifiers lets System Settings › Login Items show it under DevToolBox.app's name and icon.
    /// </summary>
    public static string MacLaunchAgent(string executable) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{MacAgentLabel}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executable)}</string>
            <string>{StartupArguments.Background}</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
          <key>ProcessType</key>
          <string>Interactive</string>
          <key>AssociatedBundleIdentifiers</key>
          <array>
            <string>{MacBundleIdentifier}</string>
          </array>
        </dict>
        </plist>

        """;

    /// <summary>An XDG autostart entry (freedesktop Desktop Entry spec).</summary>
    public static string LinuxAutostartEntry(string executable) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=DevToolBox
        Comment=Offline developer utilities
        Exec={QuoteDesktopExecArgument(executable)} {StartupArguments.Background}
        Terminal=false
        X-GNOME-Autostart-enabled=true

        """;

    /// <summary>The value of the Windows Run key: quoted path plus the background switch.</summary>
    public static string WindowsRunCommand(string executable) => $"\"{executable}\" {StartupArguments.Background}";

    /// <summary>Exec arguments are double-quoted with <c>" ` $ \</c> backslash-escaped.</summary>
    private static string QuoteDesktopExecArgument(string value)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '"' or '`' or '$' or '\\')
            {
                quoted.Append('\\');
            }

            quoted.Append(c);
        }

        return quoted.Append('"').ToString();
    }

    private static string XdgConfigHome(string home)
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".config") : xdg;
    }
}

/// <summary>macOS and Linux: the login entry is a file the OS reads at the next login.</summary>
public sealed class FileLaunchAtLogin(string entryPath, string content) : ILaunchAtLogin
{
    public bool IsSupported => true;

    public bool IsEnabled => File.Exists(entryPath);

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
                File.WriteAllText(entryPath, content);
            }
            else if (File.Exists(entryPath))
            {
                File.Delete(entryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not update the login item '{entryPath}': {ex.Message}");
        }
    }
}

public sealed class UnsupportedLaunchAtLogin : ILaunchAtLogin
{
    public static readonly UnsupportedLaunchAtLogin Instance = new();

    public bool IsSupported => false;

    public bool IsEnabled => false;

    public void SetEnabled(bool enabled)
    {
    }
}

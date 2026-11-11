using DevToolBox.App.Platform;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Platform;

public sealed class LaunchAtLoginTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "devtoolbox-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Preference_FirstStart_EnablesByDefaultAndRemembersIt()
    {
        var settings = new InMemorySettingsStore();
        var launcher = new FakeLaunchAtLogin();

        new LaunchAtLoginPreference(settings, launcher).ApplyOnStartup(enableByDefault: true);

        Assert.True(launcher.IsEnabled);
        Assert.True(settings.Current.LaunchAtLogin);
    }

    [Fact]
    public void Preference_DebugFirstStart_RegistersNothingAndLeavesTheChoiceOpen()
    {
        var settings = new InMemorySettingsStore();
        var launcher = new FakeLaunchAtLogin();

        new LaunchAtLoginPreference(settings, launcher).ApplyOnStartup(enableByDefault: false);

        Assert.Equal(0, launcher.WriteCount);
        Assert.Null(settings.Current.LaunchAtLogin);
    }

    [Fact]
    public void Preference_TurnedOff_StaysOffAndRemovesTheEntry()
    {
        var settings = new InMemorySettingsStore(new AppSettings { LaunchAtLogin = false });
        var launcher = new FakeLaunchAtLogin();
        launcher.SetEnabled(true);
        var preference = new LaunchAtLoginPreference(settings, launcher);

        preference.ApplyOnStartup(enableByDefault: true);

        Assert.False(launcher.IsEnabled);
        Assert.False(preference.IsEnabled);
    }

    [Fact]
    public void Preference_Enabled_RewritesTheEntryOnEveryStart()
    {
        var settings = new InMemorySettingsStore(new AppSettings { LaunchAtLogin = true });
        var launcher = new FakeLaunchAtLogin();
        var preference = new LaunchAtLoginPreference(settings, launcher);

        preference.ApplyOnStartup(enableByDefault: true);
        preference.ApplyOnStartup(enableByDefault: true);

        Assert.Equal(2, launcher.WriteCount);
        Assert.True(launcher.IsEnabled);
    }

    [Fact]
    public void Preference_Unsupported_DoesNothing()
    {
        var settings = new InMemorySettingsStore();
        var preference = new LaunchAtLoginPreference(settings, UnsupportedLaunchAtLogin.Instance);

        preference.ApplyOnStartup(enableByDefault: true);
        preference.SetEnabled(true);

        Assert.False(preference.IsEnabled);
        Assert.Null(settings.Current.LaunchAtLogin);
    }

    [Fact]
    public void FileEntry_IsWrittenAndRemoved()
    {
        var path = Path.Combine(_directory, "autostart", "devtoolbox.desktop");
        var entry = new FileLaunchAtLogin(path, "content");

        entry.SetEnabled(true);
        Assert.True(entry.IsEnabled);
        Assert.Equal("content", File.ReadAllText(path));

        entry.SetEnabled(false);
        Assert.False(entry.IsEnabled);
        entry.SetEnabled(false);
    }

    [Fact]
    public void MacLaunchAgent_RunsTheExecutableInTheBackgroundAtLogin()
    {
        var plist = LaunchAtLogin.MacLaunchAgent("/Applications/Dev & Tools.app/Contents/MacOS/DevToolBox");

        Assert.Contains("<string>com.devtoolbox.app</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>/Applications/Dev &amp; Tools.app/Contents/MacOS/DevToolBox</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>--background</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<key>RunAtLoad</key>\n  <true/>", plist.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void MacLaunchAgent_IsAttributedToTheAppBundle()
    {
        var plist = LaunchAtLogin.MacLaunchAgent("/Applications/DevToolBox.app/Contents/MacOS/DevToolBox").ReplaceLineEndings("\n");

        Assert.Contains(
            "<key>AssociatedBundleIdentifiers</key>\n  <array>\n    <string>com.devtoolbox.app</string>\n  </array>",
            plist,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxAutostartEntry_QuotesTheExecutable()
    {
        var entry = LaunchAtLogin.LinuxAutostartEntry("/opt/dev $tools/DevToolBox");

        Assert.Contains("Exec=\"/opt/dev \\$tools/DevToolBox\" --background", entry, StringComparison.Ordinal);
        Assert.StartsWith("[Desktop Entry]", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationPath_PrefersTheAppImageFile()
    {
        Assert.Equal(
            "/home/me/Apps/DevToolBox-x86_64.AppImage",
            LaunchAtLogin.RegistrationPath("/tmp/.mount_DevToaBc123/usr/bin/DevToolBox", "/home/me/Apps/DevToolBox-x86_64.AppImage"));
        Assert.Equal("/opt/DevToolBox/DevToolBox", LaunchAtLogin.RegistrationPath("/opt/DevToolBox/DevToolBox", null));
    }

    [Fact]
    public void WindowsRunCommand_QuotesThePath()
    {
        Assert.Equal(
            "\"C:\\Program Files\\DevToolBox\\DevToolBox.exe\" --background",
            LaunchAtLogin.WindowsRunCommand(@"C:\Program Files\DevToolBox\DevToolBox.exe"));
    }
}

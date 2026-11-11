using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Platform;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.Platform;

public class AppTrayIconTests
{
    private static (TestServices Services, ShellViewModel Shell, AppTrayIcon Tray, Window Window, Func<bool> QuitCalled) Create(
        ILaunchAtLogin? launcher = null)
    {
        var services = new TestServices();
        var shell = services.Get<ShellViewModel>();
        var window = new MainWindow { DataContext = shell };
        var quit = false;
        var lifecycle = new AppWindowLifecycle(window, new FakeDockPresence(), () => quit = true);
        var launchAtLogin = new LaunchAtLoginPreference(services.Settings, launcher ?? new FakeLaunchAtLogin());
        var tray = new AppTrayIcon(shell, services.Get<IToolRegistry>(), services.Get<FavoritesService>(), lifecycle, launchAtLogin, HostPlatform.MacOS);
        return (services, shell, tray, window, () => quit);
    }

    private static List<string> Headers(NativeMenu menu) =>
        [.. menu.Items.Select(i => i is NativeMenuItemSeparator ? "—" : (i as NativeMenuItem)?.Header ?? string.Empty)];

    [AvaloniaFact]
    public void Menu_HasOpenFavoritesAllToolsAndQuit()
    {
        var (services, _, tray, _, _) = Create();
        using var _ = services;

        var menu = tray.TrayIcon.Menu!;

        Assert.Equal(["Open DevToolBox", "—", "Favorites", "No favorites yet — star a tool", "—", "All tools", "—", "Open at login", "Quit DevToolBox"], Headers(menu));
        Assert.True(MacOSProperties.GetIsTemplateIcon(tray.TrayIcon));
        var categories = ((NativeMenuItem)menu.Items[5]).Menu!;
        Assert.Equal(["Convert", "Encode / Decode", "Format & test", "Generate"], Headers(categories));
        Assert.Equal(13, categories.Items.OfType<NativeMenuItem>().Sum(c => c.Menu!.Items.Count));
    }

    [AvaloniaFact]
    public void Favorites_AppearInMenuWhenStarred()
    {
        var (services, shell, tray, _, _) = Create();
        using var _ = services;

        shell.ToggleFavorite("jwt-decoder");

        Assert.Equal(["Open DevToolBox", "—", "Favorites", "JWT decoder", "—", "All tools", "—", "Open at login", "Quit DevToolBox"], Headers(tray.TrayIcon.Menu!));
    }

    [AvaloniaFact]
    public void FavoritesChanges_KeepTheSameMenuObject()
    {
        // Regression: replacing TrayIcon.Menu crashes on macOS ("The menu being updated does not match").
        var (services, shell, tray, _, _) = Create();
        using var _ = services;
        var menu = tray.TrayIcon.Menu;

        shell.ToggleFavorite("jwt-decoder");
        shell.ToggleFavorite("uuid-generator");
        shell.ToggleFavorite("jwt-decoder");

        Assert.Same(menu, tray.TrayIcon.Menu);
        Assert.Equal(["Open DevToolBox", "—", "Favorites", "UUID generator", "—", "All tools", "—", "Open at login", "Quit DevToolBox"], Headers(tray.TrayIcon.Menu!));

        shell.ToggleFavorite("uuid-generator");
        Assert.Equal("No favorites yet — star a tool", ((NativeMenuItem)tray.TrayIcon.Menu!.Items[3]).Header);
    }

    [AvaloniaFact]
    public void ToolItem_ShowsWindowAndOpensTool_QuitItemQuits()
    {
        var (services, shell, tray, window, quitCalled) = Create();
        using var _ = services;
        var allTools = ((NativeMenuItem)tray.TrayIcon.Menu!.Items[5]).Menu!;
        var json = ((NativeMenuItem)allTools.Items[2]).Menu!.Items.OfType<NativeMenuItem>().Single(i => i.Header == "JSON formatter");

        json.Command!.Execute(null);
        UiHelpers.Settle();

        Assert.True(window.IsVisible);
        Assert.Equal("json-formatter", shell.CurrentToolPage?.Tool.Id);

        ((NativeMenuItem)tray.TrayIcon.Menu!.Items[^1]).Command!.Execute(null);
        Assert.True(quitCalled());
    }

    [AvaloniaFact]
    public void OpenAtLogin_TogglesTheLoginEntryAndCheckmark()
    {
        var launcher = new FakeLaunchAtLogin();
        var (services, _, tray, _, _) = Create(launcher);
        using var _ = services;
        var item = tray.TrayIcon.Menu!.Items.OfType<NativeMenuItem>().Single(i => i.Header == "Open at login");
        Assert.Equal(MenuItemToggleType.CheckBox, item.ToggleType);
        Assert.False(item.IsChecked);

        item.Command!.Execute(null);

        Assert.True(item.IsChecked);
        Assert.True(launcher.IsEnabled);
        Assert.True(services.Settings.Current.LaunchAtLogin);

        item.Command!.Execute(null);

        Assert.False(item.IsChecked);
        Assert.False(launcher.IsEnabled);
        Assert.False(services.Settings.Current.LaunchAtLogin);
    }

    [AvaloniaFact]
    public void OpenAtLogin_IsHiddenWhenUnsupported()
    {
        var (services, _, tray, _, _) = Create(UnsupportedLaunchAtLogin.Instance);
        using var _ = services;

        Assert.DoesNotContain("Open at login", Headers(tray.TrayIcon.Menu!));
    }
}

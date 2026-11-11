using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DevToolBox.App.Localization;
using DevToolBox.App.Shell;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Platform;

/// <summary>
/// The menu bar (macOS) / system tray (Windows, Linux) icon: open the window, jump to a favorite or any
/// tool, toggle "Open at login", or quit. Closing the window leaves the app running here. On macOS the glyph is a template image, so the system tints it for light/dark menu bars.
/// </summary>
public sealed class AppTrayIcon : IDisposable, ILanguageAware
{
    private readonly ShellViewModel _shell;
    private readonly IToolRegistry _registry;
    private readonly FavoritesService _favorites;
    private readonly AppWindowLifecycle _lifecycle;
    private readonly LaunchAtLoginPreference _launchAtLogin;
    private readonly TrayIcon _trayIcon;
    private readonly NativeMenu _menu;
    private readonly List<NativeMenuItem> _favoriteItems = [];

    /// <summary>Menu items whose header is fixed text, with how to re-read it after a language switch.</summary>
    private readonly List<(NativeMenuItem Item, Func<string> Header)> _labeledItems = [];

    public AppTrayIcon(
        ShellViewModel shell,
        IToolRegistry registry,
        FavoritesService favorites,
        AppWindowLifecycle lifecycle,
        LaunchAtLoginPreference launchAtLogin,
        HostPlatform platform)
    {
        _shell = shell;
        _registry = registry;
        _favorites = favorites;
        _lifecycle = lifecycle;
        _launchAtLogin = launchAtLogin;

        _menu = BuildMenu();
        _trayIcon = new TrayIcon
        {
            ToolTipText = "DevToolBox",
            Icon = LoadIcon(platform == HostPlatform.MacOS ? "TrayIcon-macOS.png" : "TrayIcon.png"),
            Menu = _menu,
        };
        if (platform == HostPlatform.MacOS)
        {
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        }

        _trayIcon.Clicked += (_, _) => ShowWindow();
        _favorites.Changed += OnFavoritesChanged;
        Localizer.Register(this);
    }

    public TrayIcon TrayIcon => _trayIcon;

    /// <summary>Registers the icon with the application so it appears in the menu bar / tray.</summary>
    public void Show(Application application) => TrayIcon.SetIcons(application, [_trayIcon]);

    public void Dispose()
    {
        _favorites.Changed -= OnFavoritesChanged;
        _trayIcon.Dispose();
    }

    /// <summary>Open · Favorites · All tools ▸ (by category) · Open at login ✓ · Quit.</summary>
    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        menu.Add(Labeled(Item(Strings.Tray_Open, ShowWindow), () => Strings.Tray_Open));
        menu.Add(new NativeMenuItemSeparator());

        menu.Add(Labeled(new NativeMenuItem(Strings.Home_Favorites) { IsEnabled = false }, () => Strings.Home_Favorites));
        foreach (var item in CreateFavoriteItems())
        {
            _favoriteItems.Add(item);
            menu.Add(item);
        }

        menu.Add(new NativeMenuItemSeparator());
        var allTools = new NativeMenu();
        foreach (var group in _registry.All.GroupBy(t => t.Category).OrderBy(g => g.Key))
        {
            var category = new NativeMenu();
            foreach (var tool in group)
            {
                category.Add(Labeled(ToolItem(tool), tool.GetName));
            }

            var key = group.Key;
            allTools.Add(Labeled(new NativeMenuItem(key.DisplayName()) { Menu = category }, () => key.DisplayName()));
        }

        menu.Add(Labeled(new NativeMenuItem(Strings.Home_Title) { Menu = allTools }, () => Strings.Home_Title));
        menu.Add(new NativeMenuItemSeparator());
        if (_launchAtLogin.IsSupported)
        {
            menu.Add(LaunchAtLoginItem());
        }

        menu.Add(Labeled(Item(Strings.Tray_Quit, _lifecycle.Quit), () => Strings.Tray_Quit));
        return menu;
    }

    /// <summary>Relabels the existing items in place (the menu object itself must never be replaced).</summary>
    void ILanguageAware.OnLanguageChanged()
    {
        foreach (var (item, header) in _labeledItems)
        {
            item.Header = header();
        }

        OnFavoritesChanged(this, EventArgs.Empty);
    }

    private NativeMenuItem Labeled(NativeMenuItem item, Func<string> header)
    {
        _labeledItems.Add((item, header));
        return item;
    }

    /// <summary>
    /// Swaps only the favorite entries inside the existing menu. The tray's menu object must never be replaced:
    /// Avalonia's macOS backend throws "The menu being updated does not match" if TrayIcon.Menu is reassigned.
    /// </summary>
    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        var insertAt = _menu.Items.IndexOf(_favoriteItems[0]);
        foreach (var old in _favoriteItems)
        {
            _menu.Items.Remove(old);
        }

        _favoriteItems.Clear();
        foreach (var item in CreateFavoriteItems())
        {
            _favoriteItems.Add(item);
            _menu.Items.Insert(insertAt++, item);
        }
    }

    private List<NativeMenuItem> CreateFavoriteItems()
    {
        var favorites = _favorites.FavoriteIds.Select(_registry.Find).OfType<ToolDefinition>().ToList();
        return favorites.Count == 0
            ? [new NativeMenuItem(Strings.Tray_NoFavorites) { IsEnabled = false }]
            : [.. favorites.Select(ToolItem)];
    }

    private NativeMenuItem ToolItem(ToolDefinition tool) => Item(tool.Name, () =>
    {
        ShowWindow();
        _shell.OpenTool(tool.Id);
    });

    private static NativeMenuItem Item(string header, Action action) =>
        new(header) { Command = new CommunityToolkit.Mvvm.Input.RelayCommand(action) };

    private NativeMenuItem LaunchAtLoginItem()
    {
        var item = new NativeMenuItem(Strings.Tray_OpenAtLogin)
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _launchAtLogin.IsEnabled,
        };
        item.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            _launchAtLogin.SetEnabled(!_launchAtLogin.IsEnabled);
            item.IsChecked = _launchAtLogin.IsEnabled;
        });
        return Labeled(item, () => Strings.Tray_OpenAtLogin);
    }

    private void ShowWindow() => _lifecycle.ShowWindow();

    private static WindowIcon LoadIcon(string asset)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://DevToolBox/Assets/{asset}"));
        return new WindowIcon(stream);
    }
}

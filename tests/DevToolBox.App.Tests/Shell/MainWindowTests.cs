using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.Shell;

/// <summary>Headless UI tests of the real MainWindow, driven with pointer and keyboard input.</summary>
public class MainWindowTests
{
    private readonly InMemorySettingsStore _settings = new();

    private (MainWindow Window, ShellViewModel Shell) Show(IReadOnlyList<ToolDefinition>? tools = null)
    {
        var shell = new ShellViewModel(
            new ToolRegistry(tools ?? FakeTools.Catalog),
            new FakeToolViewModelFactory(),
            new FavoritesService(_settings),
            new FakeThemeService(),
            new LanguageService(_settings),
            _settings);
        var window = new MainWindow { DataContext = shell };
        window.Show();
        UiHelpers.Settle();
        return (window, shell);
    }

    [AvaloniaFact]
    public void Home_ShowsOneCardPerTool_AndNoFavoritesHint()
    {
        var (window, _) = Show();

        var cards = window.Descendants<Button>().Where(b => b.Classes.Contains("card")).ToList();
        Assert.Equal(3, cards.Count);
        Assert.True(window.Named<TextBlock>("NoFavoritesHint").IsVisible);
        Assert.Equal("All tools", window.Named<TextBlock>("HomeTitle").Text);
        Assert.False(window.FindControl<Border>("ContentHeader")!.IsVisible);
    }

    [AvaloniaFact]
    public void ClickingCard_OpensToolPage_AndHomeButtonReturns()
    {
        var (window, shell) = Show();

        window.Click(window.CardNamed("UUID Generator"));

        Assert.Equal("UUID Generator", window.FindControl<TextBlock>("PageTitle")!.Text);
        Assert.Equal("UUID Generator description", window.FindControl<TextBlock>("PageDescription")!.Text);
        Assert.Equal("uuid", shell.SelectedSidebarItem?.ToolId);
        Assert.True(window.FindControl<Button>("CurrentToolFavoriteButton")!.IsVisible);

        window.Click(window.FindControl<Button>("HomeButton")!);
        Assert.IsType<HomeViewModel>(shell.CurrentPage);
        Assert.False(window.FindControl<Border>("ContentHeader")!.IsVisible);
    }

    [AvaloniaFact]
    public void TypingInSearch_FiltersSidebarList()
    {
        var (window, _) = Show();

        window.TypeInto(window.FindControl<TextBox>("SearchBox")!, "epoch");

        var rows = window.FindControl<ListBox>("SidebarList")!.Items.Cast<SidebarItemViewModel>().ToList();
        Assert.Equal(["Convert", "Timestamp Converter"], rows.Select(r => r.Title));
    }

    [AvaloniaFact]
    public void StarInHeader_AddsToolToHomeFavorites()
    {
        var (window, _) = Show();
        window.Click(window.CardNamed("JWT Decoder"));

        window.Click(window.FindControl<Button>("CurrentToolFavoriteButton")!);
        window.Click(window.FindControl<Button>("HomeButton")!);

        var favorites = window.Named<ItemsControl>("FavoritesList");
        Assert.True(favorites.IsVisible);
        Assert.Equal(["jwt-decoder"], favorites.Items.Cast<ToolCardViewModel>().Select(c => c.ToolId));
        Assert.Equal(["jwt-decoder"], _settings.Current.FavoriteToolIds);
    }

    [AvaloniaFact]
    public void SidebarToggle_HidesAndRestoresSidebar()
    {
        var (window, _) = Show();
        var sidebar = window.FindControl<Border>("Sidebar")!;
        var toggle = window.FindControl<Button>("SidebarToggleButton")!;

        window.Click(toggle);
        Assert.False(sidebar.IsVisible);
        Assert.Contains("sidebar-collapsed", window.Classes);
        Assert.True(_settings.Current.IsSidebarCollapsed);

        window.Click(toggle);
        Assert.True(sidebar.IsVisible);
        Assert.DoesNotContain("sidebar-collapsed", window.Classes);
    }

    [AvaloniaFact]
    public void ToolView_IsReused_WhenNavigatingBackToTool()
    {
        var (window, shell) = Show();
        var host = window.FindControl<ContentControl>("PageHost")!;

        shell.OpenTool("timestamp");
        UiHelpers.Settle();
        var firstView = host.Presenter?.Child;
        shell.GoHome();
        UiHelpers.Settle();
        shell.OpenTool("timestamp");
        UiHelpers.Settle();

        Assert.NotNull(firstView);
        Assert.Same(firstView, host.Presenter?.Child);
    }
}

public class MainWindowTitleBarTests
{
    [AvaloniaFact]
    public void Header_IsNativeTitleBar_AndItsControlsStayClickable()
    {
        var settings = new InMemorySettingsStore();
        var shell = new ShellViewModel(new ToolRegistry(FakeTools.Catalog), new FakeToolViewModelFactory(),
            new FavoritesService(settings), new FakeThemeService(), new LanguageService(settings), settings);
        var window = new MainWindow { DataContext = shell };
        window.Show();
        UiHelpers.Settle();

        // The OS drags the window from empty header areas and handles double-click (zoom) itself.
        Assert.Equal(WindowDecorationsElementRole.TitleBar,
            WindowDecorationProperties.GetElementRole(window.FindControl<Border>("AppHeader")!));
        foreach (var name in new[] { "SidebarToggleButton", "HomeButton", "SearchField", "ThemeButton" })
        {
            Assert.Equal(WindowDecorationsElementRole.User, WindowDecorationProperties.GetElementRole(window.FindControl<Control>(name)!));
        }
    }
}

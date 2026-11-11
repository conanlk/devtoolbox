using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.Shell;

public class ShellViewModelTests
{
    private readonly InMemorySettingsStore _settings = new();
    private readonly FakeToolViewModelFactory _factory = new();
    private readonly FakeThemeService _theme = new();

    private ShellViewModel CreateShell() => new(
        new ToolRegistry(FakeTools.Catalog), _factory, new FavoritesService(_settings), _theme, new LanguageService(_settings), _settings);

    private static List<string> SidebarTitles(ShellViewModel shell) => [.. shell.SidebarItems.Select(i => i.Title)];

    [Fact]
    public void VersionLabel_ShowsVersionAndBuildNumber()
    {
        var shell = CreateShell();

        Assert.Equal($"v{AppVersion.Number} (build {AppVersion.Build})", shell.VersionLabel);
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Number);
    }

    [Fact]
    public void StartsOnHome_WithHomeSelected()
    {
        var shell = CreateShell();

        Assert.IsType<HomeViewModel>(shell.CurrentPage);
        Assert.Equal(SidebarItemKind.Home, shell.SelectedSidebarItem?.Kind);
        Assert.False(shell.IsToolPage);
    }

    [Fact]
    public void Sidebar_GroupsToolsUnderCategoryHeaders_InCategoryOrder()
    {
        var shell = CreateShell();

        Assert.Equal(
            ["All tools", "Convert", "Timestamp Converter", "Encode / Decode", "JWT Decoder", "Generate", "UUID Generator"],
            SidebarTitles(shell));
        Assert.All(shell.SidebarItems.Where(i => i.IsHeader), header => Assert.False(header.IsSelectable));
    }

    [Theory]
    [InlineData("jwt", "JWT Decoder")]
    [InlineData("EPOCH", "Timestamp Converter")]
    [InlineData("uuid gen", "UUID Generator")]
    public void Search_FiltersByNameOrKeyword(string query, string expectedTool)
    {
        var shell = CreateShell();

        shell.SearchText = query;

        var tools = shell.SidebarItems.Where(i => i.IsTool).Select(i => i.Title).ToList();
        Assert.Equal([expectedTool], tools);
        Assert.DoesNotContain(shell.SidebarItems, i => i.Kind == SidebarItemKind.Home);
    }

    [Fact]
    public void Search_NoMatch_LeavesSidebarEmpty_AndClearingRestoresAll()
    {
        var shell = CreateShell();

        shell.SearchText = "does-not-exist";
        Assert.Empty(shell.SidebarItems);

        shell.SearchText = string.Empty;
        Assert.Equal(7, shell.SidebarItems.Count);
    }

    [Fact]
    public void OpenFirstSearchResult_OpensFirstMatchingTool()
    {
        var shell = CreateShell();
        shell.SearchText = "converter";

        shell.OpenFirstSearchResultCommand.Execute(null);

        Assert.Equal("timestamp", shell.CurrentToolPage?.Tool.Id);
    }

    [Fact]
    public void SelectingSidebarTool_NavigatesToTool()
    {
        var shell = CreateShell();

        shell.SelectedSidebarItem = shell.SidebarItems.First(i => i.ToolId == "uuid");

        Assert.Equal("UUID Generator", shell.CurrentPage.Title);
        Assert.True(shell.IsToolPage);
    }

    [Fact]
    public void OpenTool_CreatesViewModelOnce_AndKeepsItAcrossNavigation()
    {
        var shell = CreateShell();

        shell.OpenTool("jwt-decoder");
        var first = shell.CurrentToolPage!.ToolViewModel;
        shell.GoHome();
        shell.OpenTool("jwt-decoder");

        Assert.Same(first, shell.CurrentToolPage!.ToolViewModel);
        Assert.Equal(1, _factory.CreatedCount);
    }

    [Fact]
    public void OpenTool_UnknownId_IsIgnored()
    {
        var shell = CreateShell();

        shell.OpenTool("nope");

        Assert.IsType<HomeViewModel>(shell.CurrentPage);
    }

    [Fact]
    public void ToggleFavorite_UpdatesSidebarHomeToolPageAndSettings()
    {
        var shell = CreateShell();

        shell.ToggleFavorite("uuid");
        shell.ToggleFavorite("jwt-decoder");

        var home = Assert.IsType<HomeViewModel>(shell.CurrentPage);
        Assert.Equal(["uuid", "jwt-decoder"], home.Favorites.Select(c => c.ToolId));
        Assert.True(shell.SidebarItems.Single(i => i.ToolId == "uuid").IsFavorite);
        Assert.Equal(["uuid", "jwt-decoder"], _settings.Current.FavoriteToolIds);

        shell.OpenTool("uuid");
        Assert.True(shell.CurrentToolPage!.IsFavorite);
        shell.ToggleCurrentToolFavoriteCommand.Execute(null);
        Assert.False(shell.CurrentToolPage!.IsFavorite);
        Assert.Equal(["jwt-decoder"], _settings.Current.FavoriteToolIds);
    }

    [Fact]
    public void Home_IgnoresFavoritesForToolsThatNoLongerExist()
    {
        _settings.Current.FavoriteToolIds.AddRange(["removed-tool", "uuid"]);

        var home = Assert.IsType<HomeViewModel>(CreateShell().CurrentPage);

        Assert.Equal(["uuid"], home.Favorites.Select(c => c.ToolId));
    }

    [Fact]
    public void ToggleSidebar_FlipsStateAndPersists()
    {
        var shell = CreateShell();

        shell.ToggleSidebarCommand.Execute(null);

        Assert.True(shell.IsSidebarCollapsed);
        Assert.True(_settings.Current.IsSidebarCollapsed);
        Assert.True(CreateShell().IsSidebarCollapsed);
    }

    [Fact]
    public void CycleTheme_GoesSystemLightDarkSystem()
    {
        var shell = CreateShell();
        var seen = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            shell.CycleThemeCommand.Execute(null);
            seen.Add($"{shell.ThemePreference}:{shell.ThemeIconKey}");
        }

        Assert.Equal(["Light:Sun", "Dark:Moon", "System:Monitor"], seen);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Shell;

/// <summary>
/// Main window state: sidebar (search, favorites, collapse), navigation between Home and tools,
/// theme and UI language. Tool view models are created on first visit and cached so tools keep their state.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, ILanguageAware
{
    private readonly IToolRegistry _registry;
    private readonly IToolViewModelFactory _toolFactory;
    private readonly FavoritesService _favorites;
    private readonly IThemeService _theme;
    private readonly ILanguageService _language;
    private readonly ISettingsStore _settings;
    private readonly Dictionary<string, ToolPageViewModel> _toolPages = new(StringComparer.Ordinal);
    private readonly SidebarItemViewModel _homeItem = SidebarItemViewModel.Home();
    private readonly List<SidebarItemViewModel> _toolItems;
    private readonly HomeViewModel _home;
    private bool _isSyncingSelection;

    public ShellViewModel(
        IToolRegistry registry,
        IToolViewModelFactory toolFactory,
        FavoritesService favorites,
        IThemeService theme,
        ILanguageService language,
        ISettingsStore settings,
        TimeProvider? time = null)
    {
        _registry = registry;
        _toolFactory = toolFactory;
        _favorites = favorites;
        _theme = theme;
        _language = language;
        _settings = settings;

        _toolItems = [.. registry.All.Select(tool => SidebarItemViewModel.Tool(
            tool.Id, tool.GetShortName, tool.Badge, new RelayCommand(() => ToggleFavorite(tool.Id))))];
        _home = new HomeViewModel(
            [.. registry.All.Select(CreateCard)],
            new SmartPasteViewModel(registry, (id, text, intent) => OpenToolWithInput(id, text, intent), time ?? TimeProvider.System));

        IsSidebarCollapsed = settings.Current.IsSidebarCollapsed;
        _favorites.Changed += (_, _) => OnFavoritesChanged();
        _theme.PreferenceChanged += (_, _) => OnPropertyChanged(nameof(ThemeIconKey));
        Localizer.Register(this);

        RefreshFavoriteFlags();
        ApplySidebarFilter();
        GoHome();
    }

    public ObservableCollection<SidebarItemViewModel> SidebarItems { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SidebarItemViewModel? SelectedSidebarItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToolPage), nameof(CurrentToolPage), nameof(CurrentDescription))]
    public partial PageViewModel CurrentPage { get; private set; } = null!;

    [ObservableProperty]
    public partial bool IsSidebarCollapsed { get; private set; }

    public bool IsToolPage => CurrentPage is ToolPageViewModel;

    public ToolPageViewModel? CurrentToolPage => CurrentPage as ToolPageViewModel;

    public ThemePreference ThemePreference => _theme.Preference;

    public string ThemeIconKey => _theme.Preference switch
    {
        ThemePreference.Light => "Sun",
        ThemePreference.Dark => "Moon",
        _ => "Monitor",
    };

    /// <summary>"Theme: Dark (click to change)".</summary>
    public string ThemeToolTip => Localizer.Format(Strings.Shell_ThemeTip, _theme.Preference switch
    {
        ThemePreference.Light => Strings.Theme_Light,
        ThemePreference.Dark => Strings.Theme_Dark,
        _ => Strings.Theme_System,
    });

    public AppLanguage Language => _language.Language;

    public bool IsEnglish => Language == AppLanguage.English;

    public bool IsVietnamese => Language == AppLanguage.Vietnamese;

    /// <summary>Sidebar footer, e.g. "v0.1.0 (build 12)".</summary>
    public string VersionLabel => Localizer.Format(Strings.Shell_Version, AppVersion.Number, AppVersion.Build);

    public HomeViewModel Home => _home;

    [RelayCommand]
    public void GoHome()
    {
        _home.SetFavorites(_favorites.FavoriteIds);
        CurrentPage = _home;
        SyncSelection(_homeItem);
    }

    /// <summary>Opens a tool and hands it content (Smart paste), when the tool accepts input.</summary>
    public void OpenToolWithInput(string toolId, string input, string? intent)
    {
        OpenTool(toolId);
        if (CurrentToolPage?.Tool.Id == toolId && CurrentToolPage.ToolViewModel is IAcceptsInput target)
        {
            target.AcceptInput(input, intent);
        }
    }

    [RelayCommand]
    public void OpenTool(string? toolId)
    {
        if (toolId is null || _registry.Find(toolId) is not { } tool)
        {
            return;
        }

        if (!_toolPages.TryGetValue(tool.Id, out var page))
        {
            page = new ToolPageViewModel(tool, _toolFactory.Create(tool)) { IsFavorite = _favorites.IsFavorite(tool.Id) };
            _toolPages[tool.Id] = page;
        }

        CurrentPage = page;
        SyncSelection(_toolItems.Find(item => item.ToolId == tool.Id));
    }

    /// <summary>Enter in the search box opens the first matching tool.</summary>
    [RelayCommand]
    public void OpenFirstSearchResult() =>
        OpenTool(SidebarItems.FirstOrDefault(item => item.IsTool)?.ToolId);

    [RelayCommand]
    public void ToggleFavorite(string? toolId)
    {
        if (toolId is not null)
        {
            _favorites.Toggle(toolId);
        }
    }

    [RelayCommand]
    public void ToggleCurrentToolFavorite() => ToggleFavorite(CurrentToolPage?.Tool.Id);

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        _settings.Update(s => s.IsSidebarCollapsed = IsSidebarCollapsed);
    }

    /// <summary>Cycles System → Light → Dark → System.</summary>
    [RelayCommand]
    public void CycleTheme()
    {
        var next = _theme.Preference switch
        {
            ThemePreference.System => ThemePreference.Light,
            ThemePreference.Light => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
        _theme.SetPreference(next);
        OnPropertyChanged(nameof(ThemePreference));
        OnPropertyChanged(nameof(ThemeToolTip));
    }

    /// <summary>Header language switcher: relabels the whole UI in place and remembers the choice.</summary>
    [RelayCommand]
    public void SetLanguage(AppLanguage language) => _language.SetLanguage(language);

    void ILanguageAware.OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsVietnamese));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(ThemeToolTip));
        OnPropertyChanged(nameof(CurrentDescription));

        _homeItem.RefreshText();
        foreach (var item in _toolItems)
        {
            item.RefreshText();
        }

        ApplySidebarFilter();
        _home.RefreshText();
        foreach (var page in _toolPages.Values)
        {
            page.RefreshText();
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySidebarFilter();

    partial void OnSelectedSidebarItemChanged(SidebarItemViewModel? value)
    {
        if (_isSyncingSelection || value is null)
        {
            return;
        }

        switch (value.Kind)
        {
            case SidebarItemKind.Home:
                GoHome();
                break;
            case SidebarItemKind.Tool:
                OpenTool(value.ToolId);
                break;
            case SidebarItemKind.Header:
            default:
                break;
        }
    }

    private void SyncSelection(SidebarItemViewModel? item)
    {
        _isSyncingSelection = true;
        try
        {
            // Hidden by the search filter → clear selection rather than point at an invisible row.
            SelectedSidebarItem = item is not null && SidebarItems.Contains(item) ? item : null;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void ApplySidebarFilter()
    {
        var current = SelectedSidebarItem;
        SidebarItems.Clear();

        var isSearching = !string.IsNullOrWhiteSpace(SearchText);
        if (!isSearching)
        {
            SidebarItems.Add(_homeItem);
        }

        foreach (var group in _registry.All
                     .Where(tool => ToolSearch.Matches(tool, SearchText))
                     .GroupBy(tool => tool.Category)
                     .OrderBy(group => group.Key))
        {
            SidebarItems.Add(SidebarItemViewModel.Header(group.Key.DisplayName()));
            foreach (var tool in group)
            {
                SidebarItems.Add(_toolItems.Find(item => item.ToolId == tool.Id)!);
            }
        }

        SyncSelection(current ?? (CurrentPage is HomeViewModel ? _homeItem : null));
    }

    private ToolCardViewModel CreateCard(ToolDefinition tool) => new(tool, OpenToolCommand);

    /// <summary>Description shown under the page title.</summary>
    public string? CurrentDescription => CurrentToolPage?.Tool.Description;

    private void OnFavoritesChanged()
    {
        RefreshFavoriteFlags();
        _home.SetFavorites(_favorites.FavoriteIds);
    }

    private void RefreshFavoriteFlags()
    {
        foreach (var item in _toolItems)
        {
            item.IsFavorite = _favorites.IsFavorite(item.ToolId!);
        }

        foreach (var page in _toolPages.Values)
        {
            page.IsFavorite = _favorites.IsFavorite(page.Tool.Id);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Shell;

public enum SidebarItemKind
{
    Home,
    Header,
    Tool,
}

public sealed partial class SidebarItemViewModel : ObservableObject
{
    private readonly Func<string> _title;

    private SidebarItemViewModel(SidebarItemKind kind, Func<string> title, string? badge, string? toolId, IRelayCommand? toggleFavorite)
    {
        Kind = kind;
        _title = title;
        Badge = badge;
        ToolId = toolId;
        ToggleFavoriteCommand = toggleFavorite;
    }

    public SidebarItemKind Kind { get; }

    public string Title => _title();

    /// <summary>Uppercase group label for headers.</summary>
    public string HeaderText => Title.ToUpperInvariant();

    /// <summary>Screen-reader name of the row's star button.</summary>
    public string FavoriteAutomationName => Localizer.Format(Strings.Shell_FavoriteTool, Title);

    public string? Badge { get; }

    public string? ToolId { get; }

    public IRelayCommand? ToggleFavoriteCommand { get; }

    public bool IsHeader => Kind == SidebarItemKind.Header;

    public bool IsSelectable => !IsHeader;

    public bool IsTool => Kind == SidebarItemKind.Tool;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    /// <summary>Re-reads the localized title after a language switch.</summary>
    public void RefreshText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(FavoriteAutomationName));
    }

    public static SidebarItemViewModel Home() => new(SidebarItemKind.Home, () => Strings.Home_Title, "ALL", null, null);

    public static SidebarItemViewModel Header(string title) => new(SidebarItemKind.Header, () => title, null, null, null);

    public static SidebarItemViewModel Tool(string toolId, Func<string> name, string badge, IRelayCommand toggleFavorite) =>
        new(SidebarItemKind.Tool, name, badge, toolId, toggleFavorite);
}

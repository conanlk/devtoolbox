using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Shell;

public sealed class ToolCardViewModel(ToolDefinition tool, IRelayCommand<string> openCommand) : ObservableObject
{
    public string ToolId => tool.Id;

    public string Name => tool.Name;

    public string Badge => tool.Badge;

    public string Description => tool.Description;

    public ToolCategory Category => tool.Category;

    public string CategoryLabel => tool.Category.ShortName().ToUpperInvariant();

    public IRelayCommand<string> OpenCommand { get; } = openCommand;

    /// <summary>Re-reads the localized name, description and category after a language switch.</summary>
    public void RefreshText() => OnPropertyChanged(string.Empty);
}

/// <summary>A Home filter chip: "All" (null category) or one category.</summary>
public sealed partial class CategoryFilter(Func<string> label, ToolCategory? category) : ObservableObject
{
    public string Label => label();

    public ToolCategory? Category { get; } = category;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IRelayCommand? SelectCommand { get; set; }

    public void RefreshText() => OnPropertyChanged(nameof(Label));
}

/// <summary>
/// Home: Smart paste, Favorites and all tools with category filter chips. Lives as long as the shell so the
/// pasted text and the chosen filter survive navigation.
/// </summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly IReadOnlyList<ToolCardViewModel> _allCards;

    public HomeViewModel(IReadOnlyList<ToolCardViewModel> cards, SmartPasteViewModel smartPaste)
    {
        _allCards = cards;
        SmartPaste = smartPaste;
        Filters = [new(() => Strings.Home_FilterAll, null), .. Enum.GetValues<ToolCategory>().Select(c => new CategoryFilter(() => c.ShortName(), c))];
        foreach (var filter in Filters)
        {
            filter.SelectCommand = new RelayCommand(() => SelectedFilter = filter);
        }

        SelectedFilter = Filters[0];
    }

    public override string Title => Strings.Home_Title;

    public string Subtitle => Localizer.Format(Strings.Home_Subtitle, _allCards.Count);

    public SmartPasteViewModel SmartPaste { get; }

    public IReadOnlyList<CategoryFilter> Filters { get; }

    [ObservableProperty]
    public partial CategoryFilter SelectedFilter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ToolCardViewModel> VisibleCards { get; private set; } = [];

    public ObservableCollection<ToolCardViewModel> Favorites { get; } = [];

    [ObservableProperty]
    public partial bool HasFavorites { get; private set; }

    public override void RefreshText()
    {
        base.RefreshText();
        OnPropertyChanged(nameof(Subtitle));
        foreach (var card in _allCards)
        {
            card.RefreshText();
        }

        foreach (var filter in Filters)
        {
            filter.RefreshText();
        }

        SmartPaste.Analyze();
    }

    public void SetFavorites(IEnumerable<string> favoriteIds)
    {
        Favorites.Clear();
        foreach (var card in favoriteIds.Select(id => _allCards.FirstOrDefault(c => c.ToolId == id)).OfType<ToolCardViewModel>())
        {
            Favorites.Add(card);
        }

        HasFavorites = Favorites.Count > 0;
    }

    partial void OnSelectedFilterChanged(CategoryFilter value)
    {
        foreach (var filter in Filters)
        {
            filter.IsSelected = filter == value;
        }

        VisibleCards = value.Category is { } category ? [.. _allCards.Where(c => c.Category == category)] : _allCards;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Shell;

/// <summary>Something the content area can show: Home or a tool page.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    public abstract string Title { get; }

    /// <summary>Re-reads localized text after a language switch.</summary>
    public virtual void RefreshText() => OnPropertyChanged(nameof(Title));
}

public sealed partial class ToolPageViewModel(ToolDefinition tool, ToolViewModelBase toolViewModel) : PageViewModel
{
    public ToolDefinition Tool { get; } = tool;

    public ToolViewModelBase ToolViewModel { get; } = toolViewModel;

    public override string Title => Tool.Name;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }
}

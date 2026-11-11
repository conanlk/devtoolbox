using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.App.Tools;
using DevToolBox.Core.Tools.SmartPaste;

namespace DevToolBox.App.Shell;

public sealed record SuggestionViewModel(string Label, bool IsPrimary, IRelayCommand Command);

/// <summary>
/// Home's "Smart paste" box. It analyzes only what the user pastes or types into it — the clipboard is
/// never read on its own (brief §5: no automatic clipboard access).
/// </summary>
public sealed partial class SmartPasteViewModel : ObservableObject, IDisposable
{
    private readonly IToolRegistry _registry;
    private readonly Action<string, string, string?> _openTool;
    private readonly TimeProvider _time;
    private readonly Debouncer _debouncer;

    /// <param name="openTool">Opens a tool with input: (toolId, text, intent).</param>
    public SmartPasteViewModel(IToolRegistry registry, Action<string, string, string?> openTool, TimeProvider time, TimeSpan? debounce = null)
    {
        _registry = registry;
        _openTool = openTool;
        _time = time;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
    }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedText))]
    public partial string? DetectedKind { get; private set; }

    /// <summary>"Detected: JWT" in the current language.</summary>
    public string? DetectedText => DetectedKind is null ? null : Localizer.Format(Strings.Home_Detected, DetectedKind);

    [ObservableProperty]
    public partial IReadOnlyList<SuggestionViewModel> Suggestions { get; private set; } = [];

    public void Dispose() => _debouncer.Dispose();

    /// <summary>Re-runs detection immediately.</summary>
    public void Analyze()
    {
        var detection = ContentDetector.Detect(Text, _time.GetUtcNow());
        var text = Text;
        DetectedKind = detection.Suggestions.Count == 0 ? null : detection.KindLabel;
        Suggestions = [.. detection.Suggestions
            .Where(s => _registry.Find(s.ToolId) is not null)
            .Select((s, i) => new SuggestionViewModel(
                i == 0 ? s.Label + " →" : s.Label,
                i == 0,
                new RelayCommand(() => _openTool(s.ToolId, text.Trim(), s.Intent))))];
    }

    partial void OnTextChanged(string value) => _debouncer.Run(Analyze);
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Json;
using DevToolBox.Core.Tools.Json;

namespace DevToolBox.App.Tools.Json;

public sealed partial class JsonViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private const int LargeInputChars = 1_000_000;
    private const int AutoExpandLimit = 50;
    private readonly Debouncer _debouncer;
    private readonly Debouncer _largeDebouncer;
    private readonly LatestTaskRunner _runner = new();
    private readonly HashSet<JsonTreeNode> _expanded = [];
    private JsonTreeNode? _root;
    private JsonFormatResult? _formatted;

    public JsonViewModel(TimeSpan? debounce = null)
    {
        _debouncer = new Debouncer(debounce ?? TimeSpan.FromMilliseconds(250));
        _largeDebouncer = new Debouncer(debounce ?? TimeSpan.FromMilliseconds(600));
        SelectedIndent = Indents[0];
    }

    public IReadOnlyList<Choice<JsonIndent>> Indents { get; } =
    [
        new(() => Strings.Json_TwoSpaces, JsonIndent.TwoSpaces),
        new(() => Strings.Json_FourSpaces, JsonIndent.FourSpaces),
        new(() => Strings.Json_Tabs, JsonIndent.Tab),
        new(() => Strings.Json_Minify, JsonIndent.Minified),
    ];

    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Choice<JsonIndent> SelectedIndent { get; set; }

    [ObservableProperty]
    public partial bool SortKeys { get; set; }

    /// <summary>Indent as an enum, for the segmented control.</summary>
    public JsonIndent Indent
    {
        get => SelectedIndent.Value;
        set => SelectedIndent = Indents.Single(i => i.Value == value);
    }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    /// <summary>"7 top-level keys · depth 2".</summary>
    [ObservableProperty]
    public partial string? StatsText { get; private set; }

    /// <summary>"231 B → 318 B".</summary>
    [ObservableProperty]
    public partial string? SizeText { get; private set; }

    [ObservableProperty]
    public partial string Output { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial ErrorLocation? ErrorLocation { get; private set; }

    [ObservableProperty]
    public partial string? Status { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<JsonTreeRowViewModel> TreeRows { get; private set; } = [];

    /// <summary>Completes when the latest formatting run has finished (tests await this).</summary>
    public Task LastRun { get; private set; } = Task.CompletedTask;

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        _largeDebouncer.Dispose();
        _runner.Dispose();
    }

    /// <summary>Formats the current input now.</summary>
    public Task FormatNowAsync()
    {
        var input = Input;
        var options = new JsonFormatOptions(SelectedIndent.Value, SortKeys);
        if (string.IsNullOrWhiteSpace(input))
        {
            _runner.Cancel();
            Apply(null);
            return LastRun = Task.CompletedTask;
        }

        IsBusy = true;
        return LastRun = _runner.RunAsync(token => JsonFormatter.Format(input, options, token), result => Apply(result));
    }

    [RelayCommand]
    private void LoadSample() => Input =
        """{"id":12345678901234567890,"name":"DevToolBox","version":1.0,"offline":true,"tags":["json","jwt","regex"],"owner":{"name":"Nguyễn Văn A","email":"a@example.com","roles":[]},"settings":null}""";

    [RelayCommand]
    private void ExpandAll()
    {
        if (_root is null)
        {
            return;
        }

        var stack = new Stack<JsonTreeNode>([_root]);
        while (stack.TryPop(out var node))
        {
            if (!node.IsContainer)
            {
                continue;
            }

            _expanded.Add(node);
            foreach (var child in Children(node))
            {
                stack.Push(child.Node);
            }
        }

        RebuildTree();
    }

    [RelayCommand]
    private void CollapseAll()
    {
        _expanded.Clear();
        if (_root is not null)
        {
            _expanded.Add(_root);
        }

        RebuildTree();
    }

    /// <summary>Relabels status and tree rows in place (keeping expansion); an error is re-parsed to re-localize it.</summary>
    protected override void OnLanguageChanged()
    {
        if (ErrorMessage is not null)
        {
            _ = FormatNowAsync();
        }
        else if (_formatted is not null)
        {
            ShowStats(_formatted);
            RebuildTree();
        }
    }

    partial void OnInputChanged(string value) =>
        (value.Length > LargeInputChars ? _largeDebouncer : _debouncer).Run(() => _ = FormatNowAsync());

    partial void OnSelectedIndentChanged(Choice<JsonIndent> value)
    {
        OnPropertyChanged(nameof(Indent));
        _ = FormatNowAsync();
    }

    partial void OnSortKeysChanged(bool value) => _ = FormatNowAsync();

    private void Apply(Result<JsonFormatResult>? result)
    {
        IsBusy = false;
        if (result is not { } r)
        {
            Output = string.Empty;
            ErrorMessage = null;
            ErrorLocation = null;
            Status = null;
            StatsText = SizeText = null;
            IsValid = false;
            _formatted = null;
            SetRoot(null);
            return;
        }

        if (!r.IsOk)
        {
            ErrorMessage = r.Error.Message;
            ErrorLocation = r.Error.Location;
            Status = null;
            IsValid = false;
            return;
        }

        ErrorMessage = null;
        ErrorLocation = null;
        IsValid = true;
        Output = r.Value.Text;
        _formatted = r.Value;
        ShowStats(r.Value);
        SetRoot(r.Value.Root);
    }

    private void ShowStats(JsonFormatResult formatted)
    {
        var root = formatted.Root;
        var top = root.Kind switch
        {
            JsonNodeKind.Object => Localizer.Format(root.ChildCount == 1 ? Strings.Json_TopLevelKey : Strings.Json_TopLevelKeys, root.ChildCount),
            JsonNodeKind.Array => Localizer.Format(root.ChildCount == 1 ? Strings.Json_Item : Strings.Json_Items, root.ChildCount),
            _ => root.Kind.ToString().ToLowerInvariant(),
        };
        StatsText = Localizer.Format(Strings.Json_Stats, top, formatted.MaxDepth);
        SizeText = $"{FormatBytes(System.Text.Encoding.UTF8.GetByteCount(Input))} → {FormatBytes(System.Text.Encoding.UTF8.GetByteCount(Output))}";
        Status = Localizer.Format(Strings.Json_Status, formatted.NodeCount, FormatSize(formatted.Text.Length));
    }

    private void SetRoot(JsonTreeNode? root)
    {
        _root = root;
        _expanded.Clear();
        if (root is not null)
        {
            // Root starts expanded; its direct children too when there are only a few of them.
            _expanded.Add(root);
            if (root.ChildCount <= AutoExpandLimit)
            {
                foreach (var child in Children(root).Where(c => c.Node.IsContainer && c.Node.ChildCount <= AutoExpandLimit))
                {
                    _expanded.Add(child.Node);
                }
            }
        }

        RebuildTree();
    }

    private void RebuildTree()
    {
        var rows = new List<JsonTreeRowViewModel>();
        if (_root is not null)
        {
            AddRows(rows, _root, "root", 0);
        }

        TreeRows = rows;
    }

    private void AddRows(List<JsonTreeRowViewModel> rows, JsonTreeNode node, string label, int depth)
    {
        var expanded = _expanded.Contains(node);
        rows.Add(new JsonTreeRowViewModel(node, label, depth, expanded, new RelayCommand(() => Toggle(node))));
        if (!expanded)
        {
            return;
        }

        foreach (var (childLabel, child) in Children(node))
        {
            AddRows(rows, child, childLabel, depth + 1);
        }
    }

    private void Toggle(JsonTreeNode node)
    {
        if (!_expanded.Remove(node))
        {
            _expanded.Add(node);
        }

        RebuildTree();
    }

    private static IEnumerable<(string Label, JsonTreeNode Node)> Children(JsonTreeNode node) =>
        node.Kind == JsonNodeKind.Object
            ? node.Properties!.Select(p => (p.Key, p.Value))
            : node.Items?.Select((item, i) => ($"[{i}]", item)) ?? [];

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.##} MB"),
    };

    private static string FormatSize(int chars) => chars switch
    {
        < 1024 => Localizer.Format(Strings.Json_Chars, chars),
        < 1024 * 1024 => Localizer.Format(Strings.Json_KChars, chars / 1024.0),
        _ => Localizer.Format(Strings.Json_MChars, chars / (1024.0 * 1024)),
    };

    public void AcceptInput(string text, string? intent)
    {
        Input = text;
        _ = FormatNowAsync();
    }
}

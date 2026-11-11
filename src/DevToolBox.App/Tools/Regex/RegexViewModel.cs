using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.App.Shared.Controls;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.RegularExpressions;

namespace DevToolBox.App.Tools.Regex;

public sealed record RegexMatchRow(string Number, string Position, string Value, string Groups);

public sealed record RegexReferenceRow(string Token, string Description);

public sealed partial class RegexViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private const int MaxHighlights = 1000;
    private readonly Debouncer _debouncer;
    private readonly LatestTaskRunner _runner = new();

    public RegexViewModel(TimeSpan? debounce = null)
    {
        _debouncer = new Debouncer(debounce ?? TimeSpan.FromMilliseconds(200));
    }

    [ObservableProperty]
    public partial string Pattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TestText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IgnoreCase { get; set; }

    [ObservableProperty]
    public partial bool Multiline { get; set; }

    [ObservableProperty]
    public partial bool Singleline { get; set; }

    [ObservableProperty]
    public partial bool IgnorePatternWhitespace { get; set; }

    [ObservableProperty]
    public partial bool ExplicitCapture { get; set; }

    [ObservableProperty]
    public partial bool RightToLeft { get; set; }

    [ObservableProperty]
    public partial bool CultureInvariant { get; set; }

    [ObservableProperty]
    public partial bool NonBacktracking { get; set; }

    [ObservableProperty]
    public partial bool ReplaceEnabled { get; set; }

    [ObservableProperty]
    public partial string Replacement { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReplaceResult { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<RegexMatchRow> Matches { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<HighlightSpan> Highlights { get; private set; } = [];

    [ObservableProperty]
    public partial string? Summary { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>"3 matches · 2 groups · 1 ms · .NET flavor".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = Strings.Regex_Flavor;

    public bool HasMatches => Matches.Count > 0;

    /// <summary>A compact .NET regex cheat sheet shown beside the results.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RegexReferenceRow> QuickReference { get; private set; } = BuildQuickReference();

    private static List<RegexReferenceRow> BuildQuickReference() =>
    [
        new(@"\d \w \s", Strings.Regex_Ref_Classes),
        new(@"\D \W \S", Strings.Regex_Ref_NegatedClasses),
        new(".", Strings.Regex_Ref_Dot),
        new("[abc] [^a-z]", Strings.Regex_Ref_CharacterClass),
        new("* + ?", Strings.Regex_Ref_Quantifiers),
        new("{n,m}", Strings.Regex_Ref_Range),
        new("^ $", Strings.Regex_Ref_Anchors),
        new(@"\b", Strings.Regex_Ref_WordBoundary),
        new("( )", Strings.Regex_Ref_Group),
        new("(?<name> )", Strings.Regex_Ref_NamedGroup),
        new("(?: )", Strings.Regex_Ref_NonCapturing),
        new("(?= ) (?! )", Strings.Regex_Ref_Lookahead),
        new("(?<= ) (?<! )", Strings.Regex_Ref_Lookbehind),
        new(@"\1  \k<name>", Strings.Regex_Ref_Backreference),
    ];
    public Task LastRun { get; private set; } = Task.CompletedTask;

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        _runner.Dispose();
    }

    /// <summary>Runs the pattern now (off the UI thread, with a timeout).</summary>
    public Task RunNowAsync()
    {
        if (Pattern.Length == 0)
        {
            _runner.Cancel();
            Matches = [];
            Highlights = [];
            Summary = null;
            ErrorMessage = null;
            ReplaceResult = string.Empty;
            StatusText = Strings.Regex_Flavor;
            OnPropertyChanged(nameof(HasMatches));
            return LastRun = Task.CompletedTask;
        }

        var pattern = Pattern;
        var input = TestText;
        var options = BuildOptions();
        var replacement = ReplaceEnabled ? Replacement : null;
        return LastRun = _runner.RunAsync(
            _ =>
            {
                var watch = Stopwatch.StartNew();
                var result = RegexTester.Run(pattern, input, options, replacement);
                return (Result: result, Elapsed: watch.Elapsed);
            },
            run => Apply(run.Result, run.Elapsed));
    }

    protected override void OnLanguageChanged()
    {
        QuickReference = BuildQuickReference();
        _ = RunNowAsync();
    }

    [RelayCommand]
    private void LoadSample()
    {
        Pattern = @"(?<user>[\w.+-]+)@(?<domain>[\w-]+\.[\w.]+)";
        TestText = "Contact: alice@example.com, bob.smith+dev@mail.co.uk\nInvalid: carol@, @nobody.com";
        Replacement = "${user} at ${domain}";
    }

    partial void OnPatternChanged(string value) => Schedule();

    partial void OnTestTextChanged(string value) => Schedule();

    partial void OnReplacementChanged(string value) => Schedule();

    partial void OnReplaceEnabledChanged(bool value) => Schedule();

    partial void OnIgnoreCaseChanged(bool value) => Schedule();

    partial void OnMultilineChanged(bool value) => Schedule();

    partial void OnSinglelineChanged(bool value) => Schedule();

    partial void OnIgnorePatternWhitespaceChanged(bool value) => Schedule();

    partial void OnExplicitCaptureChanged(bool value) => Schedule();

    partial void OnRightToLeftChanged(bool value) => Schedule();

    partial void OnCultureInvariantChanged(bool value) => Schedule();

    partial void OnNonBacktrackingChanged(bool value) => Schedule();

    private void Schedule() => _debouncer.Run(() => _ = RunNowAsync());

    private RegexOptions BuildOptions()
    {
        var options = RegexOptions.None;
        if (IgnoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (Multiline)
        {
            options |= RegexOptions.Multiline;
        }

        if (Singleline)
        {
            options |= RegexOptions.Singleline;
        }

        if (IgnorePatternWhitespace)
        {
            options |= RegexOptions.IgnorePatternWhitespace;
        }

        if (ExplicitCapture)
        {
            options |= RegexOptions.ExplicitCapture;
        }

        if (RightToLeft)
        {
            options |= RegexOptions.RightToLeft;
        }

        if (CultureInvariant)
        {
            options |= RegexOptions.CultureInvariant;
        }

        if (NonBacktracking)
        {
            options |= RegexOptions.NonBacktracking;
        }

        return options;
    }

    private void Apply(Result<RegexRunResult> result, TimeSpan elapsed)
    {
        if (!result.IsOk)
        {
            ErrorMessage = result.Error.Location?.Offset is { } offset
                ? Localizer.Format(Strings.Regex_AtPosition, result.Error.Message, offset)
                : result.Error.Message;
            Matches = [];
            Highlights = [];
            Summary = null;
            ReplaceResult = string.Empty;
            StatusText = Strings.Regex_Flavor;
            OnPropertyChanged(nameof(HasMatches));
            return;
        }

        var run = result.Value;
        ErrorMessage = null;
        Matches = [.. run.Matches.Select((m, i) => new RegexMatchRow(
            (i + 1).ToString(CultureInfo.InvariantCulture),
            $"{m.Index}–{m.Index + m.Length}",
            m.Value,
            string.Join("   ", m.Groups.Where(g => g.Success).Select(g => $"{g.Name}: {g.Value}"))))];

        // Spans must be sorted by start for the editor (RightToLeft returns matches in reverse).
        Highlights = [.. run.Matches.Take(MaxHighlights)
            .Where(m => m.Length > 0)
            .OrderBy(m => m.Index)
            .Select((m, i) => new HighlightSpan(m.Index, m.Length, i))];
        var count = run.Matches.Count;
        var countText = count.ToString(CultureInfo.InvariantCulture) + (run.IsTruncated ? "+" : string.Empty);
        Summary = count == 0 ? Strings.Regex_NoMatches
            : Localizer.Format(count == 1 ? Strings.Regex_Match : Strings.Regex_Matches, countText)
              + (run.IsTruncated ? Localizer.Format(Strings.Regex_ShowingFirst, count) : string.Empty);
        ReplaceResult = run.ReplaceResult ?? string.Empty;
        var groups = Localizer.Format(run.GroupNames.Count == 1 ? Strings.Regex_Group : Strings.Regex_Groups, run.GroupNames.Count);
        StatusText = Localizer.Format(Strings.Regex_Status, Summary, groups, elapsed.TotalMilliseconds, Strings.Regex_Flavor);
        OnPropertyChanged(nameof(HasMatches));
    }

    public void AcceptInput(string text, string? intent) => TestText = text;
}

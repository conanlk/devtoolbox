using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Controls;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.Password;
using DevToolBox.Core.Tools.Timestamp;

namespace DevToolBox.App.Tools.Password;

public enum PasswordMode
{
    Password,
    Passphrase,
}

/// <summary>A previously generated value, masked; kept in memory only for this session.</summary>
public sealed partial class HistoryEntry(string value, DateTimeOffset createdAt) : ObservableObject
{
    public string Value { get; } = value;

    public string Masked { get; } = Mask(value);

    public DateTimeOffset CreatedAt { get; } = createdAt;

    [ObservableProperty]
    public partial string Ago { get; set; } = RelativeTime.Format(createdAt, createdAt);

    private static string Mask(string value) =>
        value.Length <= 6 ? new string('•', value.Length) : value[..3] + new string('•', Math.Min(16, value.Length - 5)) + value[^2..];
}

public sealed partial class PasswordViewModel : ToolViewModelBase
{
    private const int MaxHistory = 20;
    private readonly TimeProvider _time;
    private readonly bool _ready;
    private DateTimeOffset _currentCreatedAt;
    private PasswordBatch? _batch;

    public PasswordViewModel()
        : this(TimeProvider.System)
    {
    }

    public PasswordViewModel(TimeProvider time)
    {
        _time = time;
        SelectedSeparator = Separators[0];
        _ready = true;
        Generate();
    }

    public int MinLength => PasswordGenerator.MinLength;

    public int MaxLength => PasswordGenerator.MaxLength;

    // Space first: a few EFF words contain hyphens ("t-shirt"), which makes "-" ambiguous as a separator.
    public IReadOnlyList<Choice<string>> Separators { get; } =
    [
        new(() => Strings.Password_SeparatorSpace, " "),
        new(() => Strings.Password_SeparatorHyphen, "-"),
        new(() => Strings.Password_SeparatorDot, "."),
        new(() => Strings.Password_SeparatorUnderscore, "_"),
        new(() => Strings.Password_SeparatorNone, string.Empty),
    ];

    [ObservableProperty]
    public partial PasswordMode Mode { get; set; } = PasswordMode.Password;

    public bool IsPasswordMode => Mode == PasswordMode.Password;

    public bool IsPassphraseMode => Mode == PasswordMode.Passphrase;

    // ----- Password options -----

    [ObservableProperty]
    public partial double Length { get; set; } = 20;

    [ObservableProperty]
    public partial bool Uppercase { get; set; } = true;

    [ObservableProperty]
    public partial bool Lowercase { get; set; } = true;

    [ObservableProperty]
    public partial bool Digits { get; set; } = true;

    [ObservableProperty]
    public partial bool Symbols { get; set; } = true;

    [ObservableProperty]
    public partial bool ExcludeAmbiguous { get; set; }

    [ObservableProperty]
    public partial string ExcludeCharacters { get; set; } = string.Empty;

    // ----- Passphrase options -----

    [ObservableProperty]
    public partial double WordCount { get; set; } = 5;

    [ObservableProperty]
    public partial Choice<string> SelectedSeparator { get; set; }

    [ObservableProperty]
    public partial bool Capitalize { get; set; }

    [ObservableProperty]
    public partial bool IncludeNumber { get; set; }

    // ----- Shared -----

    [ObservableProperty]
    public partial decimal? Count { get; set; } = 1;

    public ObservableCollection<string> Passwords { get; } = [];

    /// <summary>The first generated value, shown large.</summary>
    [ObservableProperty]
    public partial string Current { get; private set; } = string.Empty;

    /// <summary><see cref="Current"/> with digits and symbols colored (design: blue digits, amber symbols).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TextSegment> CurrentSegments { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasMany { get; private set; }

    [ObservableProperty]
    public partial string AllPasswords { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string StrengthText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StrengthLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string EntropyText { get; private set; } = string.Empty;

    /// <summary>0–100 (128 bits or more = full).</summary>
    [ObservableProperty]
    public partial double StrengthPercent { get; private set; }

    /// <summary>Four meter segments; filled ones are true.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<bool> StrengthBars { get; private set; } = [false, false, false, false];

    [ObservableProperty]
    public partial PasswordStrength Strength { get; private set; }

    public bool IsWeak => Strength is PasswordStrength.VeryWeak or PasswordStrength.Weak;

    public bool IsFair => Strength == PasswordStrength.Fair;

    public bool IsStrong => Strength >= PasswordStrength.Strong;

    /// <summary>Earlier values from this session (masked). Never persisted.</summary>
    public ObservableCollection<HistoryEntry> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    [RelayCommand]
    public void Generate()
    {
        if (!_ready)
        {
            return;
        }

        var count = (int)Math.Clamp(Count ?? 1, 1, PasswordGenerator.MaxCount);
        Result<PasswordBatch> result = Mode == PasswordMode.Passphrase
            ? PassphraseGenerator.Generate(new PassphraseOptions(
                (int)Math.Round(WordCount), SelectedSeparator.Value, Capitalize, IncludeNumber, count))
            : PasswordGenerator.Generate(new PasswordOptions(
                (int)Math.Round(Length), Uppercase, Lowercase, Digits, Symbols, ExcludeAmbiguous, ExcludeCharacters, count));

        RememberCurrent();
        Passwords.Clear();
        ErrorMessage = result.Error?.Message;
        _batch = result.IsOk ? result.Value : null;
        if (!result.IsOk)
        {
            Current = AllPasswords = StrengthText = StrengthLabel = EntropyText = string.Empty;
            CurrentSegments = [];
            StrengthPercent = 0;
            StrengthBars = [false, false, false, false];
            HasMany = false;
            return;
        }

        var batch = result.Value;
        foreach (var password in batch.Passwords)
        {
            Passwords.Add(password);
        }

        Current = batch.Passwords[0];
        _currentCreatedAt = _time.GetUtcNow();
        CurrentSegments = Colorize(Current);
        HasMany = batch.Passwords.Count > 1;
        AllPasswords = string.Join(Environment.NewLine, batch.Passwords);
        Strength = batch.Strength;
        StrengthPercent = Math.Min(100, batch.EntropyBits / 128 * 100);
        var filled = batch.Strength switch
        {
            PasswordStrength.VeryWeak or PasswordStrength.Weak => 1,
            PasswordStrength.Fair => 2,
            PasswordStrength.Strong => 3,
            _ => 4,
        };
        StrengthBars = [.. Enumerable.Range(0, 4).Select(i => i < filled)];
        ShowStrengthText(batch);
        OnPropertyChanged(nameof(IsWeak));
        OnPropertyChanged(nameof(IsFair));
        OnPropertyChanged(nameof(IsStrong));
    }

    /// <summary>Relabels strength and history in place — never regenerates, so the shown password stays the same.</summary>
    protected override void OnLanguageChanged()
    {
        if (_batch is { } batch)
        {
            ShowStrengthText(batch);
        }
        else
        {
            Generate();
        }

        var now = _time.GetUtcNow();
        foreach (var entry in History)
        {
            entry.Ago = RelativeTime.Format(entry.CreatedAt, now);
        }
    }

    private void ShowStrengthText(PasswordBatch batch)
    {
        StrengthLabel = Label(batch.Strength);
        EntropyText = Localizer.Format(Strings.Password_Entropy, batch.EntropyBits);
        StrengthText = Localizer.Format(Strings.Password_StrengthStatus, StrengthLabel, batch.EntropyBits, batch.PoolSize,
            Mode == PasswordMode.Passphrase ? Strings.Password_PoolWords : Strings.Password_PoolCharacters);
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
    }

    partial void OnModeChanged(PasswordMode value)
    {
        OnPropertyChanged(nameof(IsPasswordMode));
        OnPropertyChanged(nameof(IsPassphraseMode));
        Generate();
    }

    partial void OnLengthChanged(double value) => Generate();

    partial void OnUppercaseChanged(bool value) => Generate();

    partial void OnLowercaseChanged(bool value) => Generate();

    partial void OnDigitsChanged(bool value) => Generate();

    partial void OnSymbolsChanged(bool value) => Generate();

    partial void OnExcludeAmbiguousChanged(bool value) => Generate();

    partial void OnExcludeCharactersChanged(string value) => Generate();

    partial void OnWordCountChanged(double value) => Generate();

    partial void OnSelectedSeparatorChanged(Choice<string> value) => Generate();

    partial void OnCapitalizeChanged(bool value) => Generate();

    partial void OnIncludeNumberChanged(bool value) => Generate();

    partial void OnCountChanged(decimal? value) => Generate();

    private void RememberCurrent()
    {
        var now = _time.GetUtcNow();
        if (Current.Length > 0)
        {
            History.Insert(0, new HistoryEntry(Current, _currentCreatedAt));
            while (History.Count > MaxHistory)
            {
                History.RemoveAt(History.Count - 1);
            }
        }

        foreach (var entry in History)
        {
            entry.Ago = RelativeTime.Format(entry.CreatedAt, now);
        }

        OnPropertyChanged(nameof(HasHistory));
    }

    private static List<TextSegment> Colorize(string value)
    {
        var segments = new List<TextSegment>();
        var run = new StringBuilder();
        string? runKey = null;
        foreach (var c in value)
        {
            var key = char.IsAsciiDigit(c) ? "Brush.Json.Key" : char.IsLetter(c) || c == ' ' ? null : "Brush.AccentText";
            if (run.Length > 0 && key != runKey)
            {
                segments.Add(new TextSegment(run.ToString(), runKey));
                run.Clear();
            }

            runKey = key;
            run.Append(c);
        }

        if (run.Length > 0)
        {
            segments.Add(new TextSegment(run.ToString(), runKey));
        }

        return segments;
    }

    private static string Label(PasswordStrength strength) => strength switch
    {
        PasswordStrength.VeryWeak => Strings.Password_VeryWeak,
        PasswordStrength.Weak => Strings.Password_Weak,
        PasswordStrength.Fair => Strings.Password_Fair,
        PasswordStrength.Strong => Strings.Password_Strong,
        _ => Strings.Password_VeryStrong,
    };
}

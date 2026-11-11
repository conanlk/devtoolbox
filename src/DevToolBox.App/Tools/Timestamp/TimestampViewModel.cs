using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.Core.Tools.Timestamp;

namespace DevToolBox.App.Tools.Timestamp;

/// <summary>A time zone option; the "Local" entry's label is localized and relabels live.</summary>
public sealed class TimeZoneChoice : ObservableObject, ILabeled, ILanguageAware
{
    private readonly Func<string> _label;

    public TimeZoneChoice(Func<string> label, TimeZoneInfo zone)
    {
        _label = label;
        Zone = zone;
        Localizer.Register(this);
    }

    public TimeZoneChoice(string label, TimeZoneInfo zone)
    {
        _label = () => label;
        Zone = zone;
    }

    public string Label => _label();

    public TimeZoneInfo Zone { get; }

    void ILanguageAware.OnLanguageChanged() => OnPropertyChanged(nameof(Label));

    public override string ToString() => Label;
}

public sealed partial class TimestampViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private readonly TimeProvider _time;
    private readonly ITimer _clock;

    public TimestampViewModel(TimeProvider time)
    {
        _time = time;
        TimeZones = BuildTimeZones();
        SelectedZone = TimeZones[0];
        SelectedUnit = Units[0];
        UpdateClock();

        // Live "current time" ticker, marshalled to the UI thread when one exists.
        var context = SynchronizationContext.Current;
        _clock = time.CreateTimer(
            _ => Post(context, UpdateClock), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Now();
    }

    public IReadOnlyList<TimeZoneChoice> TimeZones { get; }

    public IReadOnlyList<Choice<TimestampUnit>> Units { get; } =
    [
        new(() => Strings.Timestamp_AutoDetect, TimestampUnit.Auto),
        new(() => Strings.Timestamp_Seconds, TimestampUnit.Seconds),
        new(() => Strings.Timestamp_Milliseconds, TimestampUnit.Milliseconds),
        new(() => Strings.Timestamp_Microseconds, TimestampUnit.Microseconds),
        new(() => Strings.Timestamp_Nanoseconds, TimestampUnit.Nanoseconds),
    ];

    [ObservableProperty]
    public partial TimeZoneChoice SelectedZone { get; set; }

    [ObservableProperty]
    public partial Choice<TimestampUnit> SelectedUnit { get; set; }

    [ObservableProperty]
    public partial string CurrentEpochSeconds { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentEpochMilliseconds { get; private set; } = string.Empty;

    // ----- Timestamp → date -----

    [ObservableProperty]
    public partial string EpochInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DetectedUnit { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> EpochFields { get; private set; } = [];

    [ObservableProperty]
    public partial string? EpochError { get; private set; }

    // ----- Date → timestamp -----

    [ObservableProperty]
    public partial string DateInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> DateFields { get; private set; } = [];

    [ObservableProperty]
    public partial string? DateError { get; private set; }

    public void Dispose()
    {
        StopLanguageUpdates();
        _clock.Dispose();
    }

    [RelayCommand]
    private void Now()
    {
        var now = _time.GetUtcNow();
        EpochInput = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        DateInput = TimeZoneInfo.ConvertTime(now, SelectedZone.Zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    protected override void OnLanguageChanged()
    {
        ConvertEpoch();
        ConvertDate();
    }

    partial void OnEpochInputChanged(string value) => ConvertEpoch();

    partial void OnSelectedUnitChanged(Choice<TimestampUnit> value) => ConvertEpoch();

    partial void OnDateInputChanged(string value) => ConvertDate();

    partial void OnSelectedZoneChanged(TimeZoneChoice value)
    {
        ConvertEpoch();
        ConvertDate();
    }

    private void ConvertEpoch()
    {
        EpochError = null;
        EpochFields = [];
        DetectedUnit = null;
        if (string.IsNullOrWhiteSpace(EpochInput) || SelectedUnit is null || SelectedZone is null)
        {
            return;
        }

        var result = TimestampConverter.FromEpoch(EpochInput, SelectedUnit.Value);
        if (!result.IsOk)
        {
            EpochError = result.Error.Message;
            return;
        }

        DetectedUnit = Localizer.Format(Strings.Timestamp_InterpretedAs, TimestampConverter.UnitName(result.Value.Unit));
        EpochFields = [.. TimestampConverter.Describe(result.Value.Utc, SelectedZone.Zone, _time.GetUtcNow())
            .Select(f => new LabeledValue(f.Label, f.Value))];
    }

    private void ConvertDate()
    {
        DateError = null;
        DateFields = [];
        if (string.IsNullOrWhiteSpace(DateInput) || SelectedZone is null)
        {
            return;
        }

        var result = TimestampConverter.ParseDateTime(DateInput, SelectedZone.Zone);
        if (!result.IsOk)
        {
            DateError = result.Error.Message;
            return;
        }

        var fields = TimestampConverter.Describe(result.Value, SelectedZone.Zone, _time.GetUtcNow()).ToDictionary(f => f.Key);
        DateFields =
        [
            .. new[] { "Unix seconds", "Unix milliseconds", "Unix microseconds", "Unix nanoseconds" }
                .Select(key => new LabeledValue(fields[key].Label, fields[key].Value)),
            new(Strings.Timestamp_IsoUtc, TimestampConverter.ToIso8601(result.Value.ToUniversalTime())),
        ];
    }

    private void UpdateClock()
    {
        var now = _time.GetUtcNow();
        CurrentEpochSeconds = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        CurrentEpochMilliseconds = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }

    private static void Post(SynchronizationContext? context, Action action)
    {
        if (context is null)
        {
            action();
        }
        else
        {
            context.Post(_ => action(), null);
        }
    }

    private static List<TimeZoneChoice> BuildTimeZones()
    {
        static string Label(TimeZoneInfo zone, string? name = null)
        {
            var offset = zone.BaseUtcOffset;
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return $"(UTC{sign}{offset.Duration():hh\\:mm}) {name ?? zone.Id}";
        }

        var zones = new List<TimeZoneChoice>
        {
            new(() => Label(TimeZoneInfo.Local, Localizer.Format(Strings.Timestamp_LocalZone, TimeZoneInfo.Local.Id)), TimeZoneInfo.Local),
            new("(UTC+00:00) UTC", TimeZoneInfo.Utc),
        };
        zones.AddRange(TimeZoneInfo.GetSystemTimeZones()
            .Where(z => z.Id != TimeZoneInfo.Local.Id && z.Id != TimeZoneInfo.Utc.Id)
            .OrderBy(z => z.BaseUtcOffset)
            .ThenBy(z => z.Id, StringComparer.Ordinal)
            .Select(z => new TimeZoneChoice(Label(z), z)));
        return zones;
    }

    public void AcceptInput(string text, string? intent)
    {
        if (TimestampConverter.FromEpoch(text).IsOk)
        {
            EpochInput = text.Trim();
        }
        else
        {
            DateInput = text.Trim();
        }
    }
}

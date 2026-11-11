using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.Core.Tools.Uuid;

namespace DevToolBox.App.Tools.Uuid;

public sealed partial class UuidViewModel : ToolViewModelBase, IAcceptsInput
{
    private IReadOnlyList<Guid> _guids = [];

    public UuidViewModel()
    {
        SelectedVersion = Versions[0];
        Generate();
    }

    public IReadOnlyList<Choice<UuidVersion>> Versions { get; } =
    [
        new(() => Strings.Uuid_V4, UuidVersion.V4),
        new(() => Strings.Uuid_V7, UuidVersion.V7),
    ];

    [ObservableProperty]
    public partial Choice<UuidVersion> SelectedVersion { get; set; }

    [ObservableProperty]
    public partial decimal? Count { get; set; } = 5;

    [ObservableProperty]
    public partial bool Uppercase { get; set; }

    [ObservableProperty]
    public partial bool Hyphens { get; set; } = true;

    [ObservableProperty]
    public partial bool Braces { get; set; }

    [ObservableProperty]
    public partial string Output { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    // ----- Inspector -----

    [ObservableProperty]
    public partial string InspectInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> InspectFields { get; private set; } = [];

    [ObservableProperty]
    public partial string? InspectError { get; private set; }

    [RelayCommand]
    public void Generate()
    {
        var count = (int)Math.Clamp(Count ?? 1, 1, UuidTools.MaxCount);
        var result = UuidTools.GenerateGuids(SelectedVersion.Value, count);
        ErrorMessage = result.Error?.Message;
        _guids = result.IsOk ? result.Value : [];
        Reformat();
    }

    [RelayCommand]
    private void InspectFirst() => InspectInput = _guids.Count > 0 ? UuidTools.Format(_guids[0], new UuidFormat()) : string.Empty;

    /// <summary>Re-inspects so field labels and errors follow the language; generated UUIDs are kept.</summary>
    protected override void OnLanguageChanged() => OnInspectInputChanged(InspectInput);

    partial void OnSelectedVersionChanged(Choice<UuidVersion> value) => Generate();

    partial void OnCountChanged(decimal? value) => Generate();

    partial void OnUppercaseChanged(bool value) => Reformat();

    partial void OnHyphensChanged(bool value) => Reformat();

    partial void OnBracesChanged(bool value) => Reformat();

    partial void OnInspectInputChanged(string value)
    {
        InspectError = null;
        InspectFields = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var result = UuidTools.Inspect(value);
        if (!result.IsOk)
        {
            InspectError = result.Error.Message;
            return;
        }

        var info = result.Value;
        var fields = new List<LabeledValue>
        {
            new(Strings.Uuid_Canonical, info.Canonical),
            new(Strings.Uuid_Version, info.VersionName),
            new(Strings.Uuid_Variant, info.Variant),
        };
        if (info.Timestamp is { } timestamp)
        {
            fields.Add(new(Strings.Uuid_TimestampUtc, timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'", CultureInfo.InvariantCulture)));
            fields.Add(new(Strings.Uuid_TimestampLocal, timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)));
        }

        InspectFields = fields;
    }

    private void Reformat()
    {
        var format = new UuidFormat(Uppercase, Hyphens, Braces);
        Output = string.Join(Environment.NewLine, _guids.Select(g => UuidTools.Format(g, format)));
    }

    public void AcceptInput(string text, string? intent) => InspectInput = text.Trim();
}

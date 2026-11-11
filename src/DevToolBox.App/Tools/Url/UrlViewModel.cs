using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.EncodeDecode;
using DevToolBox.Core.Tools.Url;

namespace DevToolBox.App.Tools.Url;

public sealed partial class QueryParameterViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public IRelayCommand? RemoveCommand { get; init; }
}

public sealed partial class UrlViewModel : TextTransformViewModel
{
    private ParsedUrl? _parsed;

    public UrlViewModel(TimeSpan? debounce = null)
        : base(debounce)
    {
        SelectedMode = Modes[0];
        QueryParameters.CollectionChanged += (_, _) => RebuildUrl();
    }

    public IReadOnlyList<Choice<UrlEncodeMode>> Modes { get; } =
    [
        new(() => Strings.Url_ModeComponent, UrlEncodeMode.Component),
        new(() => Strings.Url_ModeFullUri, UrlEncodeMode.FullUri),
    ];

    [ObservableProperty]
    public partial CodecDirection Direction { get; set; } = CodecDirection.Encode;

    [ObservableProperty]
    public partial Choice<UrlEncodeMode> SelectedMode { get; set; }

    /// <summary>Form encoding: space ↔ '+'.</summary>
    [ObservableProperty]
    public partial bool PlusForSpace { get; set; }

    // ----- URL parser -----

    [ObservableProperty]
    public partial string UrlToParse { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ParseError { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> UrlParts { get; private set; } = [];

    public ObservableCollection<QueryParameterViewModel> QueryParameters { get; } = [];

    [ObservableProperty]
    public partial string RebuiltUrl { get; private set; } = string.Empty;

    public bool HasParsedUrl => _parsed is not null;

    protected override string SampleInput => Direction == CodecDirection.Encode
        ? "name=Nguyễn Văn A&note=50% off / free?"
        : "name%3DNguy%E1%BB%85n%20V%C4%83n%20A%26note%3D50%25%20off%20%2F%20free%3F";

    protected override Result<string> Transform(string input) => Direction == CodecDirection.Encode
        ? UrlCodec.Encode(input, SelectedMode.Value, PlusForSpace)
        : UrlCodec.Decode(input, SelectedMode.Value, PlusForSpace);

    /// <summary>0 = Encode / Decode tab, 1 = URL parser tab.</summary>
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    public override void AcceptInput(string text, string? intent)
    {
        if (intent == "parse")
        {
            SelectedTab = 1;
            UrlToParse = text;
            return;
        }

        SelectedTab = 0;
        base.AcceptInput(text, intent);
    }

    protected override void ApplyIntent(string? intent) =>
        Direction = intent == "decode" ? CodecDirection.Decode : CodecDirection.Encode;

    protected override void OnSwapping() =>
        Direction = Direction == CodecDirection.Encode ? CodecDirection.Decode : CodecDirection.Encode;

    /// <summary>Relabels the URL parts and re-localizes a parse error without touching edited query parameters.</summary>
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        if (_parsed is not null)
        {
            UrlParts = BuildParts(_parsed);
        }
        else if (ParseError is not null)
        {
            ParseError = UrlParser.Parse(UrlToParse).Error?.Message;
        }
    }

    [RelayCommand]
    private void LoadSampleUrl() =>
        UrlToParse = "https://user@api.example.com:8443/v1/search?q=dev+tools&lang=vi&tags=json%2Cjwt#results";

    [RelayCommand]
    private void AddParameter() => QueryParameters.Add(CreateParameter(string.Empty, string.Empty));

    partial void OnDirectionChanged(CodecDirection value) => Refresh();

    partial void OnSelectedModeChanged(Choice<UrlEncodeMode> value) => Refresh();

    partial void OnPlusForSpaceChanged(bool value) => Refresh();

    partial void OnUrlToParseChanged(string value) => ParseUrl();

    private void ParseUrl()
    {
        QueryParameters.Clear();
        _parsed = null;
        ParseError = null;
        UrlParts = [];

        if (!string.IsNullOrWhiteSpace(UrlToParse))
        {
            var result = UrlParser.Parse(UrlToParse);
            if (result.IsOk)
            {
                _parsed = result.Value;
                UrlParts = BuildParts(_parsed);
                foreach (var parameter in _parsed.Query)
                {
                    QueryParameters.Add(CreateParameter(parameter.Key, parameter.Value));
                }
            }
            else
            {
                ParseError = result.Error.Message;
            }
        }

        OnPropertyChanged(nameof(HasParsedUrl));
        RebuildUrl();
    }

    private static List<LabeledValue> BuildParts(ParsedUrl url) =>
    [
        new(Strings.Url_Scheme, url.Scheme),
        new(Strings.Url_UserInfo, url.UserInfo),
        new(Strings.Url_Host, url.Host),
        new(Strings.Url_Port, url.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? Strings.Url_DefaultPort),
        new(Strings.Url_Path, url.Path),
        new(Strings.Url_Fragment, url.Fragment),
    ];

    private QueryParameterViewModel CreateParameter(string key, string value)
    {
        QueryParameterViewModel? parameter = null;
        parameter = new QueryParameterViewModel
        {
            Key = key,
            Value = value,
            RemoveCommand = new RelayCommand(() => QueryParameters.Remove(parameter!)),
        };
        parameter.PropertyChanged += OnParameterChanged;
        return parameter;
    }

    private void OnParameterChanged(object? sender, PropertyChangedEventArgs e) => RebuildUrl();

    private void RebuildUrl() =>
        RebuiltUrl = _parsed is null
            ? string.Empty
            : UrlParser.Build(_parsed with { Query = [.. QueryParameters.Select(p => new QueryParameter(p.Key, p.Value))] });
}

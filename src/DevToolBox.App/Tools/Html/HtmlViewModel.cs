using CommunityToolkit.Mvvm.ComponentModel;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.EncodeDecode;
using DevToolBox.Core.Tools.Html;

namespace DevToolBox.App.Tools.Html;

public sealed partial class HtmlViewModel : TextTransformViewModel
{
    public HtmlViewModel(TimeSpan? debounce = null)
        : base(debounce)
    {
        SelectedScope = Scopes[0];
    }

    public IReadOnlyList<Choice<HtmlEncodeScope>> Scopes { get; } =
    [
        new(() => Strings.Html_ScopeUnsafeOnly, HtmlEncodeScope.UnsafeOnly),
        new(() => Strings.Html_ScopeAllNonAscii, HtmlEncodeScope.AllNonAscii),
    ];

    [ObservableProperty]
    public partial CodecDirection Direction { get; set; } = CodecDirection.Encode;

    [ObservableProperty]
    public partial Choice<HtmlEncodeScope> SelectedScope { get; set; }

    [ObservableProperty]
    public partial bool PreferNamedEntities { get; set; } = true;

    public bool IsEncoding => Direction == CodecDirection.Encode;

    /// <summary>Status bar: "Encode" / "Decode".</summary>
    public string DirectionText => IsEncoding ? Strings.Common_Encode : Strings.Common_Decode;

    protected override string SampleInput => Direction == CodecDirection.Encode
        ? "<p class=\"note\">Tom & Jerry's café — 5 > 3 © 2026 😀</p>"
        : "&lt;p class=&quot;note&quot;&gt;Tom &amp; Jerry&#39;s caf&eacute; &mdash; 5 &gt; 3 &copy; 2026 &#x1F600;&lt;/p&gt;";

    protected override Result<string> Transform(string input) => Direction == CodecDirection.Encode
        ? HtmlEntityCodec.Encode(input, SelectedScope.Value, PreferNamedEntities)
        : HtmlEntityCodec.Decode(input);

    protected override void ApplyIntent(string? intent) =>
        Direction = intent == "decode" ? CodecDirection.Decode : CodecDirection.Encode;

    protected override void OnSwapping() =>
        Direction = Direction == CodecDirection.Encode ? CodecDirection.Decode : CodecDirection.Encode;

    partial void OnDirectionChanged(CodecDirection value)
    {
        OnPropertyChanged(nameof(IsEncoding));
        OnPropertyChanged(nameof(DirectionText));
        Refresh();
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DirectionText));
        base.OnLanguageChanged();
    }

    partial void OnSelectedScopeChanged(Choice<HtmlEncodeScope> value) => Refresh();

    partial void OnPreferNamedEntitiesChanged(bool value) => Refresh();
}

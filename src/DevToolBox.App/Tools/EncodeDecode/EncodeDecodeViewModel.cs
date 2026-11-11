using CommunityToolkit.Mvvm.ComponentModel;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.EncodeDecode;

namespace DevToolBox.App.Tools.EncodeDecode;

public enum CodecMode
{
    Auto,
    Encode,
    Decode,
}

public sealed partial class EncodeDecodeViewModel : TextTransformViewModel
{
    public EncodeDecodeViewModel(TimeSpan? debounce = null)
        : base(debounce)
    {
        SelectedEncoding = Encodings[0];
    }

    public IReadOnlyList<Choice<TextEncoding>> Encodings { get; } =
    [
        new("Base64", TextEncoding.Base64),
        new("Base64URL", TextEncoding.Base64Url),
        new("Hex", TextEncoding.Hex),
        new(() => Strings.EncodeDecode_Binary, TextEncoding.Binary),
    ];

    [ObservableProperty]
    public partial Choice<TextEncoding> SelectedEncoding { get; set; }

    [ObservableProperty]
    public partial CodecMode Mode { get; set; } = CodecMode.Auto;

    /// <summary>"Encoding" / "Decoding" — what Auto decided (or the forced direction).</summary>
    [ObservableProperty]
    public partial string DirectionText { get; private set; } = Strings.EncodeDecode_Encoding;

    protected override string SampleInput => "Hello, DevToolBox! Xin chào 👋";

    protected override Result<string> Transform(string input)
    {
        var direction = EffectiveDirection(input);
        var directionName = direction == CodecDirection.Decode ? Strings.EncodeDecode_Decoding : Strings.EncodeDecode_Encoding;
        DirectionText = Mode == CodecMode.Auto ? Localizer.Format(Strings.EncodeDecode_AutoDirection, directionName) : directionName;
        return direction == CodecDirection.Decode
            ? TextCodec.Decode(input, SelectedEncoding.Value)
            : TextCodec.Encode(input, SelectedEncoding.Value);
    }

    protected override void ApplyIntent(string? intent) => Mode = intent switch
    {
        "decode" => CodecMode.Decode,
        "encode" => CodecMode.Encode,
        _ => CodecMode.Auto,
    };

    protected override void OnSwapping() =>
        Mode = EffectiveDirection(Input) == CodecDirection.Encode ? CodecMode.Decode : CodecMode.Encode;

    private CodecDirection EffectiveDirection(string input) => Mode switch
    {
        CodecMode.Encode => CodecDirection.Encode,
        CodecMode.Decode => CodecDirection.Decode,
        _ => TextCodec.DetectDirection(input, SelectedEncoding.Value),
    };

    partial void OnSelectedEncodingChanged(Choice<TextEncoding> value) => Refresh();

    partial void OnModeChanged(CodecMode value) => Refresh();
}

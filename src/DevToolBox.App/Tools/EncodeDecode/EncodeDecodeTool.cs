using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.EncodeDecode;

public static class EncodeDecodeTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "encode-decode",
        GetName: () => Strings.Tool_EncodeDecode_Name,
        GetShortName: () => Strings.Tool_EncodeDecode_ShortName,
        GetDescription: () => Strings.Tool_EncodeDecode_Description,
        Category: ToolCategory.EncodeDecode,
        Keywords: ["base64", "base64url", "hex", "binary", "encode", "decode", "string"],
        Badge: "B64",
        CreateViewModel: _ => new EncodeDecodeViewModel(),
        CreateView: () => new EncodeDecodeView());
}

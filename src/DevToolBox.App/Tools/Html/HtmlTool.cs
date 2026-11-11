using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.Html;

public static class HtmlTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "html-encode",
        GetName: () => Strings.Tool_Html_Name,
        GetShortName: () => Strings.Tool_Html_ShortName,
        GetDescription: () => Strings.Tool_Html_Description,
        Category: ToolCategory.EncodeDecode,
        Keywords: ["html", "entities", "escape", "unescape", "encode", "decode", "xss"],
        Badge: "</>",
        CreateViewModel: _ => new HtmlViewModel(),
        CreateView: () => new HtmlView());
}

using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.Url;

public static class UrlTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "url-encode",
        GetName: () => Strings.Tool_Url_Name,
        GetShortName: () => Strings.Tool_Url_ShortName,
        GetDescription: () => Strings.Tool_Url_Description,
        Category: ToolCategory.EncodeDecode,
        Keywords: ["url", "uri", "percent", "encodeuricomponent", "query", "parse", "escape"],
        Badge: "%",
        CreateViewModel: _ => new UrlViewModel(),
        CreateView: () => new UrlView());
}

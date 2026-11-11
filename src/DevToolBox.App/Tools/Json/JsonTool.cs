using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.Json;

public static class JsonTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "json-formatter",
        GetName: () => Strings.Tool_Json_Name,
        GetShortName: () => Strings.Tool_Json_ShortName,
        GetDescription: () => Strings.Tool_Json_Description,
        Category: ToolCategory.FormatAndTest,
        Keywords: ["json", "format", "beautify", "pretty", "minify", "validate", "tree", "lint"],
        Badge: "{}",
        CreateViewModel: _ => new JsonViewModel(),
        CreateView: () => new JsonView());
}

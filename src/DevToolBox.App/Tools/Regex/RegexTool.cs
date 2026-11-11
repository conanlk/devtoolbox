using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.Regex;

public static class RegexTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "regex-tester",
        GetName: () => Strings.Tool_Regex_Name,
        GetShortName: () => Strings.Tool_Regex_ShortName,
        GetDescription: () => Strings.Tool_Regex_Description,
        Category: ToolCategory.FormatAndTest,
        Keywords: ["regex", "regular expression", "pattern", "match", "replace", "groups", "test"],
        Badge: ".*",
        CreateViewModel: _ => new RegexViewModel(),
        CreateView: () => new RegexView());
}

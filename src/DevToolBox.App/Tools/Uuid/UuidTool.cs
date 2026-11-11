using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.Uuid;

public static class UuidTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "uuid-generator",
        GetName: () => Strings.Tool_Uuid_Name,
        GetShortName: () => Strings.Tool_Uuid_ShortName,
        GetDescription: () => Strings.Tool_Uuid_Description,
        Category: ToolCategory.Generate,
        Keywords: ["uuid", "guid", "v4", "v7", "generate", "inspect", "validate"],
        Badge: "ID",
        CreateViewModel: _ => new UuidViewModel(),
        CreateView: () => new UuidView());
}

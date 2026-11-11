using DevToolBox.App.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.Timestamp;

public static class TimestampTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "timestamp",
        GetName: () => Strings.Tool_Timestamp_Name,
        GetShortName: () => Strings.Tool_Timestamp_ShortName,
        GetDescription: () => Strings.Tool_Timestamp_Description,
        Category: ToolCategory.Convert,
        Keywords: ["timestamp", "epoch", "unix", "date", "time", "iso 8601", "rfc 2822", "timezone"],
        Badge: "TS",
        CreateViewModel: services => new TimestampViewModel(services.GetRequiredService<TimeProvider>()),
        CreateView: () => new TimestampView());
}

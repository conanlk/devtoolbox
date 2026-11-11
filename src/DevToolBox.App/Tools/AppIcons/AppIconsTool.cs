using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.AppIcons;

public static class AppIconsTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "app-icons",
        GetName: () => Strings.Tool_AppIcons_Name,
        GetShortName: () => Strings.Tool_AppIcons_ShortName,
        GetDescription: () => Strings.Tool_AppIcons_Description,
        Category: ToolCategory.Generate,
        Keywords: ["app icon", "icon", "ios", "android", "iphone", "ipad", "mipmap", "adaptive", "appiconset", "react native", "launcher"],
        Badge: "ICO",
        CreateViewModel: services => new AppIconsViewModel(
            services.GetRequiredService<IFileService>(), services.GetRequiredService<IClipboardService>()),
        CreateView: () => new AppIconsView());
}

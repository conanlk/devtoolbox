using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.ImageBase64;

public static class ImageBase64Tool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "image-base64",
        GetName: () => Strings.Tool_ImageBase64_Name,
        GetShortName: () => Strings.Tool_ImageBase64_ShortName,
        GetDescription: () => Strings.Tool_ImageBase64_Description,
        Category: ToolCategory.Convert,
        Keywords: ["image", "base64", "data uri", "img", "css", "png", "jpg", "svg", "embed"],
        Badge: "IMG",
        CreateViewModel: services => new ImageBase64ViewModel(
            services.GetRequiredService<IClipboardService>(), services.GetRequiredService<IFileService>()),
        CreateView: () => new ImageBase64View());
}

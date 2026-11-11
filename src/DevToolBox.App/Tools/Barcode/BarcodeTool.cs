using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.Barcode;

public static class BarcodeTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "barcode-generator",
        GetName: () => Strings.Tool_Barcode_Name,
        GetShortName: () => Strings.Tool_Barcode_ShortName,
        GetDescription: () => Strings.Tool_Barcode_Description,
        Category: ToolCategory.Generate,
        Keywords: ["barcode", "qr", "qr code", "code 128", "ean", "upc", "code 39", "png", "svg"],
        Badge: "|||",
        CreateViewModel: services => new BarcodeViewModel(
            services.GetRequiredService<IClipboardService>(), services.GetRequiredService<IFileService>()),
        CreateView: () => new BarcodeView());
}

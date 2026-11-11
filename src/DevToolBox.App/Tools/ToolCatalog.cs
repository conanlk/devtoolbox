using DevToolBox.App.Tools.AppIcons;
using DevToolBox.App.Tools.Barcode;
using DevToolBox.App.Tools.EncodeDecode;
using DevToolBox.App.Tools.Hash;
using DevToolBox.App.Tools.Html;
using DevToolBox.App.Tools.ImageBase64;
using DevToolBox.App.Tools.Json;
using DevToolBox.App.Tools.Jwt;
using DevToolBox.App.Tools.Password;
using DevToolBox.App.Tools.Regex;
using DevToolBox.App.Tools.Timestamp;
using DevToolBox.App.Tools.Url;
using DevToolBox.App.Tools.Uuid;

namespace DevToolBox.App.Tools;

/// <summary>The single list of tools shipped in the app. Adding a tool = one line here.</summary>
public static class ToolCatalog
{
    public static IReadOnlyList<ToolDefinition> All { get; } =
    [
        // Convert
        TimestampTool.Definition,
        JwtTool.Definition,
        ImageBase64Tool.Definition,

        // Encode / Decode
        EncodeDecodeTool.Definition,
        HtmlTool.Definition,
        UrlTool.Definition,

        // Format & test
        JsonTool.Definition,
        RegexTool.Definition,

        // Generate
        PasswordTool.Definition,
        UuidTool.Definition,
        HashTool.Definition,
        BarcodeTool.Definition,
        AppIconsTool.Definition,
    ];
}

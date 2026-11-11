using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.Hash;

public static class HashTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "hash-generator",
        GetName: () => Strings.Tool_Hash_Name,
        GetShortName: () => Strings.Tool_Hash_ShortName,
        GetDescription: () => Strings.Tool_Hash_Description,
        Category: ToolCategory.Generate,
        Keywords: ["hash", "md5", "sha1", "sha256", "sha512", "sha3", "crc32", "hmac", "checksum", "file"],
        Badge: "#",
        CreateViewModel: services => new HashViewModel(services.GetRequiredService<IFileService>()),
        CreateView: () => new HashView());
}

using DevToolBox.App.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.Jwt;

public static class JwtTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "jwt-decoder",
        GetName: () => Strings.Tool_Jwt_Name,
        GetShortName: () => Strings.Tool_Jwt_ShortName,
        GetDescription: () => Strings.Tool_Jwt_Description,
        Category: ToolCategory.Convert,
        Keywords: ["jwt", "token", "jws", "bearer", "decode", "claims", "hs256", "verify"],
        Badge: "JWT",
        CreateViewModel: services => new JwtViewModel(services.GetRequiredService<TimeProvider>()),
        CreateView: () => new JwtView());
}

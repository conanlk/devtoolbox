using DevToolBox.App.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tools.Password;

public static class PasswordTool
{
    public static ToolDefinition Definition { get; } = new(
        Id: "password-generator",
        GetName: () => Strings.Tool_Password_Name,
        GetShortName: () => Strings.Tool_Password_ShortName,
        GetDescription: () => Strings.Tool_Password_Description,
        Category: ToolCategory.Generate,
        Keywords: ["password", "passphrase", "random", "secure", "generate", "entropy", "csprng"],
        Badge: "***",
        CreateViewModel: services => new PasswordViewModel(services.GetRequiredService<TimeProvider>()),
        CreateView: () => new PasswordView());
}

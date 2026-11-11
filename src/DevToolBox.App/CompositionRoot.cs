using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shell;
using DevToolBox.App.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App;

/// <summary>
/// Explicit service registration (no assembly scanning) so the app stays Native-AOT/trim safe.
/// </summary>
public static class CompositionRoot
{
    /// <param name="configure">Optional overrides (tests replace platform services with fakes).</param>
    public static ServiceProvider Build(
        ISettingsStore settingsStore,
        IReadOnlyList<ToolDefinition> tools,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(settingsStore);
        services.AddSingleton<TopLevelProvider>();
        services.AddSingleton<ITopLevelProvider>(sp => sp.GetRequiredService<TopLevelProvider>());
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IToolRegistry>(new ToolRegistry(tools));
        services.AddSingleton<IToolViewModelFactory, ServiceProviderToolViewModelFactory>();
        services.AddSingleton<FavoritesService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<ShellViewModel>();
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}

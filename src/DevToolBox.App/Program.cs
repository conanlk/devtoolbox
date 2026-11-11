using Avalonia;
using Avalonia.Media;
using DevToolBox.App.Platform;

namespace DevToolBox.App;

internal static class Program
{
    // Don't use Avalonia, third-party APIs or SynchronizationContext-reliant code before
    // AppMain is called: things aren't initialized yet.
    [STAThread]
    public static int Main(string[] args)
    {
        // Smoke tests (CI, or next to a running copy) skip the single-instance lock.
        SingleInstance? instance = null;
        if (!StartupArguments.Has(args, StartupArguments.SmokeExit))
        {
            instance = SingleInstance.TryAcquire(AppPaths.ConfigDirectory);
            if (instance is null)
            {
                // Already running: a normal launch brings its window forward; a login launch has nothing to do.
                if (!StartupArguments.Has(args, StartupArguments.Background))
                {
                    SingleInstance.SignalRunningInstance(AppPaths.ConfigDirectory);
                }

                return 0;
            }
        }

        using (instance)
        {
            return BuildAvaloniaApp(instance).StartWithClassicDesktopLifetime(args);
        }
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(null);

    private static AppBuilder BuildAvaloniaApp(SingleInstance? instance)
        => AppBuilder.Configure(() => new App(instance))
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .With(new FontManagerOptions { DefaultFamilyName = AppFonts.Sans })
            .LogToTrace();
}

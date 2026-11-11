using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shell;
using DevToolBox.App.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App;

public partial class App : Application
{
    private readonly SingleInstance? _instance;
    private ServiceProvider? _services;
    private AppTrayIcon? _trayIcon;

    public App()
    {
    }

    /// <param name="instance">The single-instance lock; later launches signal it to show the window.</param>
    public App(SingleInstance? instance) => _instance = instance;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Headless UI tests have no desktop lifetime: they build their own windows.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CrashLog.Install();
            var settings = JsonSettingsStore.Load(AppPaths.SettingsFilePath);
            _services = CompositionRoot.Build(settings, ToolCatalog.All);
            _services.GetRequiredService<IThemeService>().ApplySavedPreference();
            _services.GetRequiredService<ILanguageService>().ApplySavedPreference();

            var window = new MainWindow
            {
                DataContext = _services.GetRequiredService<ShellViewModel>(),
            };
            window.Configure(new WindowChrome(HostPlatformDetector.Current), new WindowPlacementTracker(settings));
            _services.GetRequiredService<TopLevelProvider>().TopLevel = window;

            // Closing the window hides it to the tray / menu bar; only Quit ends the app.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var isSmokeTest = StartupArguments.Has(desktop.Args, StartupArguments.SmokeExit);
            var lifecycle = new AppWindowLifecycle(window, CreateDockPresence(), () => desktop.Shutdown());
            if (StartupArguments.Has(desktop.Args, StartupArguments.Background) && !isSmokeTest)
            {
                lifecycle.HideWindow();
            }
            else
            {
                desktop.MainWindow = window;
            }

            var launchAtLogin = new LaunchAtLoginPreference(
                settings, isSmokeTest ? UnsupportedLaunchAtLogin.Instance : LaunchAtLogin.ForCurrentPlatform());
            launchAtLogin.ApplyOnStartup(enableByDefault: IsReleaseBuild);

            _trayIcon = new AppTrayIcon(
                _services.GetRequiredService<ShellViewModel>(),
                _services.GetRequiredService<IToolRegistry>(),
                _services.GetRequiredService<FavoritesService>(),
                lifecycle,
                launchAtLogin,
                HostPlatformDetector.Current);
            _trayIcon.Show(this);

            if (_instance is not null)
            {
                _instance.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(lifecycle.ShowWindow);
                _instance.ListenForActivation();
            }

            desktop.Exit += (_, _) =>
            {
                _trayIcon.Dispose();
                _services.Dispose();
            };

            if (isSmokeTest)
            {
                window.Opened += (_, _) => RunSmokeTestAndExit(desktop, window);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Debug builds don't register for login by default: they would point the OS at a bin/ folder.</summary>
#if DEBUG
    private const bool IsReleaseBuild = false;
#else
    private const bool IsReleaseBuild = true;
#endif

    /// <summary>macOS: sets the Dock icon (unbundled builds show a generic one otherwise) and hides it with the window.</summary>
    private static IDockPresence CreateDockPresence()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return NoDockPresence.Instance;
        }

        using var stream = AssetLoader.Open(new Uri("avares://DevToolBox/Assets/AppIcon-macOS.png"));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var png = memory.ToArray();
        MacDockIcon.Set(png);
        return new MacDockPresence(png);
    }

    /// <summary>
    /// Reports startup time, then opens every tool once in the real (e.g. Native AOT) build so trimming or
    /// reflection problems surface in CI. Exit code 1 if any tool fails to open.
    /// </summary>
    private static void RunSmokeTestAndExit(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        var startup = DateTime.Now - Process.GetCurrentProcess().StartTime;
        Console.WriteLine($"startup_ms={startup.TotalMilliseconds:F0}");
        Console.WriteLine($"version={AppVersion.Full}");

        Dispatcher.UIThread.Post(() =>
        {
            var shell = (ShellViewModel)window.DataContext!;
            var failures = 0;
            foreach (var tool in ToolCatalog.All)
            {
                try
                {
                    shell.OpenTool(tool.Id);
                    window.UpdateLayout();
                    if (shell.CurrentToolPage?.Tool.Id != tool.Id)
                    {
                        throw new InvalidOperationException("tool page did not open");
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine($"smoke_error={tool.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            shell.GoHome();

            // Vietnamese ships as a satellite resource assembly: prove it loads in this (e.g. AOT) build. Not persisted.
            var startLanguage = Localizer.Language;
            Localizer.SetLanguage(AppLanguage.Vietnamese);
            var vietnamese = Strings.Home_Title;
            Localizer.SetLanguage(AppLanguage.English);
            Console.WriteLine($"l10n_vi={(vietnamese != Strings.Home_Title ? "ok" : "missing")}");
            Localizer.SetLanguage(startLanguage);

            if (OperatingSystem.IsMacOS() && window.TryGetPlatformHandle() is IMacOSTopLevelPlatformHandle mac)
            {
                Console.WriteLine($"window_buttons={MacTrafficLights.Describe(mac.NSWindow)}");

                // A theme switch makes AppKit re-lay out the title bar; the buttons must stay centred. Not persisted.
                var app = Application.Current!;
                var startTheme = app.RequestedThemeVariant;
                app.RequestedThemeVariant = window.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
                DispatcherTimer.RunOnce(() =>
                {
                    if (OperatingSystem.IsMacOS())
                    {
                        Console.WriteLine($"window_buttons_after_theme={MacTrafficLights.Describe(mac.NSWindow)}");
                    }
                    app.RequestedThemeVariant = startTheme;
                    Finish();
                }, TimeSpan.FromMilliseconds(500));
                return;
            }

            Finish();

            void Finish()
            {
                Console.WriteLine($"tools_ok={ToolCatalog.All.Count - failures}/{ToolCatalog.All.Count}");
                desktop.Shutdown(failures == 0 ? 0 : 1);
            }
        }, DispatcherPriority.Background);
    }
}

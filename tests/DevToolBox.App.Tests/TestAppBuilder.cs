using Avalonia;
using Avalonia.Headless;
using DevToolBox.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace DevToolBox.App.Tests;

public static class TestAppBuilder
{
    // Real Skia rendering (not the headless stub) so image tools can be checked pixel by pixel.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

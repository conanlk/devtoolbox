using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace DevToolBox.App.Platform;

public interface IWindowChrome
{
    /// <summary>Applies OS-specific window styling. Call before the window is shown.</summary>
    void Apply(Window window);
}

/// <summary>
/// macOS: content extends under the title bar so the traffic lights sit on the sidebar.
/// Windows 11: Mica backdrop when available. Linux: standard decorations.
/// Adds a style class ("macos" / "translucent") so XAML styles can adapt layout and brushes.
/// </summary>
public sealed class WindowChrome(HostPlatform platform) : IWindowChrome
{
    public const string MacOSClass = "macos";
    public const string TranslucentClass = "translucent";

    /// <summary>Height of the app header (Themes/Colors.axaml "Size.HeaderHeight").</summary>
    public const double HeaderHeight = 48;

    public void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        switch (platform)
        {
            case HostPlatform.MacOS:
                window.ExtendClientAreaToDecorationsHint = true;
                // Make the native title bar as tall as our header so macOS centres the traffic lights in it
                // (the default ~28 px bar pins them to the top edge of the 48 px header).
                window.ExtendClientAreaTitleBarHeightHint = HeaderHeight;
                window.Classes.Add(MacOSClass);
                if (OperatingSystem.IsMacOS())
                {
                    KeepTrafficLightsCentred(window);
                }

                break;
            case HostPlatform.Windows:
                ApplyBackdrop(window);
                break;
            case HostPlatform.Linux:
            default:
                break;
        }
    }

    /// <summary>Re-centres the window buttons after opening and whenever macOS lays them out again (resize, state, theme).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static void KeepTrafficLightsCentred(Window window)
    {
        void Apply() => Dispatcher.UIThread.Post(() =>
        {
            if (window.TryGetPlatformHandle() is IMacOSTopLevelPlatformHandle handle)
            {
                MacTrafficLights.Center(handle.NSWindow, HeaderHeight);
            }
        }, DispatcherPriority.Render);

        window.Opened += (_, _) => Apply();
        window.SizeChanged += (_, _) => Apply();
        // A theme switch sets a new NSAppearance; AppKit then shrinks the title bar back to its default height.
        window.ActualThemeVariantChanged += (_, _) => Apply();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
            {
                Apply();
            }
        };
    }

    private static void ApplyBackdrop(Window window)
    {
        window.TransparencyLevelHint =
            [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];

        window.Opened += (_, _) =>
        {
            var level = window.ActualTransparencyLevel;
            if (level == WindowTransparencyLevel.Mica || level == WindowTransparencyLevel.AcrylicBlur)
            {
                window.Background = Brushes.Transparent;
                window.Classes.Add(TranslucentClass);
            }
        };
    }
}

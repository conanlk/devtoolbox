using Avalonia;
using Avalonia.Controls;

namespace DevToolBox.App.Platform;

/// <summary>Restores and remembers the main window's size, position and maximized state.</summary>
public sealed class WindowPlacementTracker(ISettingsStore settings)
{
    private const double MinWidth = 640;
    private const double MinHeight = 420;

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Restore(window);
        window.Closing += (_, _) => Save(window);
    }

    private void Restore(Window window)
    {
        if (settings.Current.Window is not { } placement)
        {
            return;
        }

        window.Width = Math.Max(MinWidth, placement.Width);
        window.Height = Math.Max(MinHeight, placement.Height);

        // Skip the saved position if that monitor is gone (e.g. laptop undocked).
        var position = new PixelPoint(placement.X, placement.Y);
        if (window.Screens.All.Any(screen => screen.WorkingArea.Contains(position)))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = position;
        }

        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private void Save(Window window)
    {
        var isMaximized = window.WindowState == WindowState.Maximized;
        var previous = settings.Current.Window;

        // While maximized, keep the last normal bounds so un-maximizing next session looks right.
        var placement = isMaximized && previous is not null
            ? previous with { IsMaximized = true }
            : new WindowPlacement(window.Position.X, window.Position.Y, window.Width, window.Height, isMaximized);

        settings.Update(s => s.Window = placement);
    }
}

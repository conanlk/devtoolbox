using Avalonia.Controls;

namespace DevToolBox.App.Platform;

/// <summary>
/// Keeps the app running in the tray / menu bar: closing the main window only hides it, and the app exits
/// only through <see cref="Quit"/> (tray menu, macOS ⌘Q) or when the OS logs out or shuts down.
/// </summary>
public sealed class AppWindowLifecycle
{
    private readonly Window _window;
    private readonly IDockPresence _dock;
    private readonly Action _shutdown;

    public AppWindowLifecycle(Window window, IDockPresence dock, Action shutdown)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _dock = dock;
        _shutdown = shutdown;
        _window.Closing += OnClosing;
    }

    public bool IsQuitting { get; private set; }

    public void ShowWindow()
    {
        _dock.SetVisible(true);
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    public void HideWindow()
    {
        _window.Hide();
        _dock.SetVisible(false);
    }

    public void Quit()
    {
        IsQuitting = true;
        _shutdown();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Application/OS shutdown must close for real; only the user closing the window goes to the tray.
        if (IsQuitting || e.CloseReason != WindowCloseReason.WindowClosing)
        {
            return;
        }

        e.Cancel = true;
        HideWindow();
    }
}

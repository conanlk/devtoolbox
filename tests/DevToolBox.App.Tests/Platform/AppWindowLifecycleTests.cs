using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Platform;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Platform;

public class AppWindowLifecycleTests
{
    private readonly FakeDockPresence _dock = new();
    private int _shutdowns;

    private (Window Window, AppWindowLifecycle Lifecycle) Create()
    {
        var window = new Window();
        var lifecycle = new AppWindowLifecycle(window, _dock, () => _shutdowns++);
        lifecycle.ShowWindow();
        UiHelpers.Settle();
        return (window, lifecycle);
    }

    [AvaloniaFact]
    public void ClosingTheWindow_HidesItToTheTray_AndKeepsTheAppRunning()
    {
        var (window, _) = Create();

        window.Close();
        UiHelpers.Settle();

        Assert.False(window.IsVisible);
        Assert.False(_dock.IsVisible);
        Assert.Equal(0, _shutdowns);
    }

    [AvaloniaFact]
    public void ShowWindow_AfterClose_ReopensTheSameWindowAndDockIcon()
    {
        var (window, lifecycle) = Create();
        window.Close();

        lifecycle.ShowWindow();
        UiHelpers.Settle();

        Assert.True(window.IsVisible);
        Assert.True(_dock.IsVisible);
    }

    [AvaloniaFact]
    public void ShowWindow_RestoresAMinimizedWindow()
    {
        var (window, lifecycle) = Create();
        window.WindowState = WindowState.Minimized;

        lifecycle.ShowWindow();

        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void Quit_ShutsDown_AndLetsTheWindowCloseForReal()
    {
        var (window, lifecycle) = Create();

        lifecycle.Quit();
        window.Close();
        UiHelpers.Settle();

        Assert.Equal(1, _shutdowns);
        Assert.True(lifecycle.IsQuitting);
        Assert.False(window.IsVisible);
        Assert.NotEqual(false, _dock.IsVisible);
    }
}

using DevToolBox.App.Platform;

namespace DevToolBox.App.Tests.TestDoubles;

internal sealed class FakeLaunchAtLogin : ILaunchAtLogin
{
    public bool IsSupported => true;

    public bool IsEnabled { get; private set; }

    public int WriteCount { get; private set; }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        WriteCount++;
    }
}

internal sealed class FakeDockPresence : IDockPresence
{
    public bool? IsVisible { get; private set; }

    public void SetVisible(bool visible) => IsVisible = visible;
}

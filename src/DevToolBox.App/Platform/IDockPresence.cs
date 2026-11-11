using System.Runtime.Versioning;

namespace DevToolBox.App.Platform;

/// <summary>Whether the app shows in the macOS Dock. Windows and Linux have no equivalent (the hidden window leaves the taskbar).</summary>
public interface IDockPresence
{
    void SetVisible(bool visible);
}

public sealed class NoDockPresence : IDockPresence
{
    public static readonly NoDockPresence Instance = new();

    public void SetVisible(bool visible)
    {
    }
}

/// <summary>Menu-bar-only while the window is closed: the Dock icon disappears and comes back on open.</summary>
[SupportedOSPlatform("macos")]
public sealed class MacDockPresence(byte[] iconPng) : IDockPresence
{
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            MacDockIcon.ShowInDock();

            // An unbundled executable falls back to the generic icon whenever it re-enters the Dock.
            MacDockIcon.Set(iconPng);
        }
        else
        {
            MacDockIcon.HideFromDock();
        }
    }
}

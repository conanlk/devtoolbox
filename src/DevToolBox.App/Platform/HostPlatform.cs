namespace DevToolBox.App.Platform;

public enum HostPlatform
{
    Windows,
    MacOS,
    Linux,
}

public static class HostPlatformDetector
{
    public static HostPlatform Current =>
        OperatingSystem.IsWindows() ? HostPlatform.Windows
        : OperatingSystem.IsMacOS() ? HostPlatform.MacOS
        : HostPlatform.Linux;
}

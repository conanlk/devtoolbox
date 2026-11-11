using System.Globalization;

namespace DevToolBox.App.Platform;

/// <summary>The running app's version from version.json, stamped into the assembly as major.minor.patch.build.</summary>
public static class AppVersion
{
    private static readonly Version Assembly = typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>major.minor.patch, e.g. 0.1.0.</summary>
    public static string Number { get; } = Assembly.ToString(3);

    /// <summary>The build counter, increased by every publish.</summary>
    public static int Build { get; } = Math.Max(Assembly.Revision, 0);

    /// <summary>major.minor.patch.build, for logs and machine-readable output.</summary>
    public static string Full { get; } = string.Create(CultureInfo.InvariantCulture, $"{Number}.{Build}");
}

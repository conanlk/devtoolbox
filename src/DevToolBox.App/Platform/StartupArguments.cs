namespace DevToolBox.App.Platform;

/// <summary>Command-line switches the app understands.</summary>
public static class StartupArguments
{
    /// <summary>Starts with only the tray / menu bar icon. Used by the launch-at-login entry.</summary>
    public const string Background = "--background";

    /// <summary>Starts the app, reports startup time on stdout, then exits. Used by CI smoke tests.</summary>
    public const string SmokeExit = "--smoke-exit";

    public static bool Has(IEnumerable<string>? args, string argument) =>
        args?.Contains(argument, StringComparer.Ordinal) == true;
}

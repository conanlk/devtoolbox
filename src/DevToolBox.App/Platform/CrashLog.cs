using System.Globalization;
using Avalonia.Threading;

namespace DevToolBox.App.Platform;

/// <summary>
/// Writes unhandled exceptions to stderr and to &lt;config folder&gt;/logs/crash.log, so a crash leaves a readable
/// report (the macOS crash report has no managed stack). Local file only — nothing is sent anywhere.
/// </summary>
public static class CrashLog
{
    public static string FilePath => Path.Combine(AppPaths.ConfigDirectory, "logs", "crash.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("AppDomain", e.ExceptionObject as Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) => Write("UI thread", e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("Task", e.Exception);
    }

    public static void Write(string source, Exception? exception)
    {
        var entry = string.Create(CultureInfo.InvariantCulture,
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Unhandled exception ({source}): {exception}{Environment.NewLine}");
        Console.Error.Write(entry);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, entry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never cause a second failure.
        }
    }
}

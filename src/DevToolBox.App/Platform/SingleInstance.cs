using System.Diagnostics;
using System.Globalization;

namespace DevToolBox.App.Platform;

/// <summary>
/// One running app per user. The first instance holds an exclusive lock on a file in the config directory
/// (released by the OS even after a crash); a later launch touches a signal file instead, which makes the
/// first instance show its window. File-based so it needs no sockets or pipes.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string LockFileName = "instance.lock";
    private const string SignalFileName = "activate.signal";

    private readonly FileStream? _lock;
    private readonly string _directory;
    private FileSystemWatcher? _watcher;

    private SingleInstance(FileStream? lockStream, string directory)
    {
        _lock = lockStream;
        _directory = directory;
    }

    /// <summary>Raised on a thread-pool thread when another launch asks this instance to show itself.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>Returns the lock, or null when another instance already holds it.</summary>
    public static SingleInstance? TryAcquire(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Can't tell: run anyway rather than refuse to start.
            Trace.TraceWarning($"Single-instance check skipped: {ex.Message}");
            return new SingleInstance(null, directory);
        }

        try
        {
            var stream = new FileStream(
                Path.Combine(directory, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstance(stream, directory);
        }
        catch (IOException)
        {
            // Sharing violation (Windows) or held flock (macOS/Linux): another instance is running.
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceWarning($"Single-instance check skipped: {ex.Message}");
            return new SingleInstance(null, directory);
        }
    }

    /// <summary>Asks the running instance to show its window.</summary>
    public static void SignalRunningInstance(string directory)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(directory, SignalFileName),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not signal the running instance: {ex.Message}");
        }
    }

    /// <summary>Starts watching for <see cref="SignalRunningInstance"/> from later launches.</summary>
    public void ListenForActivation()
    {
        if (_watcher is not null)
        {
            return;
        }

        _watcher = new FileSystemWatcher(_directory, SignalFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnSignal;
        _watcher.Created += OnSignal;
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _lock?.Dispose();
    }

    private void OnSignal(object sender, FileSystemEventArgs e) => ActivationRequested?.Invoke(this, EventArgs.Empty);
}

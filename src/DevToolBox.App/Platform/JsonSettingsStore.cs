using System.Diagnostics;
using System.Text.Json;

namespace DevToolBox.App.Platform;

/// <summary>Settings stored as a JSON file; writes are atomic (temp file + move).</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly Lock _gate = new();

    private JsonSettingsStore(string filePath, AppSettings current)
    {
        _filePath = filePath;
        Current = current;
    }

    public AppSettings Current { get; }

    /// <summary>Loads settings, falling back to defaults when the file is missing or unreadable.</summary>
    public static JsonSettingsStore Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new JsonSettingsStore(filePath, ReadOrDefault(filePath));
    }

    public void Update(Action<AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            change(Current);
            Save();
        }
    }

    private static AppSettings ReadOrDefault(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt settings file must never prevent the app from starting.
            Trace.TraceWarning($"Ignoring unreadable settings file '{filePath}': {ex.Message}");
            return new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, Current, AppSettingsJsonContext.Default.AppSettings);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is acceptable; crashing the UI is not.
            Trace.TraceWarning($"Could not save settings to '{_filePath}': {ex.Message}");
        }
    }
}

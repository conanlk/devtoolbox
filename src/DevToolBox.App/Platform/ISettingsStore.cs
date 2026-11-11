namespace DevToolBox.App.Platform;

public interface ISettingsStore
{
    AppSettings Current { get; }

    /// <summary>Applies a change and persists it.</summary>
    void Update(Action<AppSettings> change);
}

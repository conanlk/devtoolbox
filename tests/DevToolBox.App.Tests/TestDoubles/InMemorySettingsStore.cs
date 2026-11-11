using DevToolBox.App.Platform;

namespace DevToolBox.App.Tests.TestDoubles;

internal sealed class InMemorySettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Current { get; } = initial ?? new AppSettings();

    public int SaveCount { get; private set; }

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        SaveCount++;
    }
}

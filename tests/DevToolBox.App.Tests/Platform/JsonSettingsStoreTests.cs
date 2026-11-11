using DevToolBox.App.Platform;

namespace DevToolBox.App.Tests.Platform;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "devtoolbox-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "nested", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = JsonSettingsStore.Load(FilePath);

        Assert.Equal(ThemePreference.System, store.Current.Theme);
        Assert.Empty(store.Current.FavoriteToolIds);
        Assert.Null(store.Current.Window);
    }

    [Fact]
    public void Update_PersistsAndReloads()
    {
        var store = JsonSettingsStore.Load(FilePath);

        store.Update(s =>
        {
            s.Theme = ThemePreference.Dark;
            s.FavoriteToolIds.Add("jwt-decoder");
            s.IsSidebarCollapsed = true;
            s.Window = new WindowPlacement(10, 20, 900, 600, IsMaximized: true);
        });

        var reloaded = JsonSettingsStore.Load(FilePath).Current;
        Assert.Equal(ThemePreference.Dark, reloaded.Theme);
        Assert.Equal(["jwt-decoder"], reloaded.FavoriteToolIds);
        Assert.True(reloaded.IsSidebarCollapsed);
        Assert.Equal(new WindowPlacement(10, 20, 900, 600, true), reloaded.Window);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Theme_IsStoredAsReadableString()
    {
        JsonSettingsStore.Load(FilePath).Update(s => s.Theme = ThemePreference.Light);

        Assert.Contains("\"Light\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    public void Load_CorruptFile_FallsBackToDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);

        var store = JsonSettingsStore.Load(FilePath);

        Assert.Equal(ThemePreference.System, store.Current.Theme);
    }
}

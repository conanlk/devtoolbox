using DevToolBox.App.Platform;

namespace DevToolBox.App.Shell;

/// <summary>Favorite tool ids, in the order they were starred, persisted to settings.</summary>
public sealed class FavoritesService(ISettingsStore settings)
{
    public event EventHandler? Changed;

    public IReadOnlyList<string> FavoriteIds => settings.Current.FavoriteToolIds;

    public bool IsFavorite(string toolId) => settings.Current.FavoriteToolIds.Contains(toolId, StringComparer.Ordinal);

    public void Toggle(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        settings.Update(s =>
        {
            if (!s.FavoriteToolIds.Remove(toolId))
            {
                s.FavoriteToolIds.Add(toolId);
            }
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

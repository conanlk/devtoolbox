using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace DevToolBox.App.Platform;

public interface IClipboardService
{
    Task SetTextAsync(string text);

    Task<string?> GetTextAsync();

    Task SetBitmapAsync(Bitmap bitmap);

    Task<Bitmap?> GetBitmapAsync();
}

public sealed class ClipboardService(ITopLevelProvider topLevel) : IClipboardService
{
    public Task SetTextAsync(string text) =>
        Clipboard is { } clipboard ? clipboard.SetTextAsync(text) : Task.CompletedTask;

    public async Task<string?> GetTextAsync() =>
        Clipboard is { } clipboard ? await clipboard.TryGetTextAsync().ConfigureAwait(true) : null;

    public Task SetBitmapAsync(Bitmap bitmap) =>
        Clipboard is { } clipboard ? clipboard.SetBitmapAsync(bitmap) : Task.CompletedTask;

    public async Task<Bitmap?> GetBitmapAsync() =>
        Clipboard is { } clipboard ? await clipboard.TryGetBitmapAsync().ConfigureAwait(true) : null;

    private IClipboard? Clipboard => topLevel.TopLevel?.Clipboard;
}

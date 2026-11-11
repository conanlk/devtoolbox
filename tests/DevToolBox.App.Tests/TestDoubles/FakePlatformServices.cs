using Avalonia.Media.Imaging;
using DevToolBox.App.Platform;

namespace DevToolBox.App.Tests.TestDoubles;

internal sealed class FakeClipboardService : IClipboardService
{
    public string? Text { get; set; }

    public Bitmap? Bitmap { get; set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);

    public Task SetBitmapAsync(Bitmap bitmap)
    {
        Bitmap = bitmap;
        return Task.CompletedTask;
    }

    public Task<Bitmap?> GetBitmapAsync() => Task.FromResult(Bitmap);
}

/// <summary>Returns a preset file for "open" and captures whatever a "save" writes.</summary>
internal sealed class FakeFileService : IFileService
{
    public PickedFile? FileToOpen { get; set; }

    public string? SavedName { get; private set; }

    public byte[]? SavedBytes { get; private set; }

    public static PickedFile InMemory(string name, byte[] bytes) =>
        new(name, bytes.Length, () => Task.FromResult<Stream>(new MemoryStream(bytes)));

    public Task<PickedFile?> OpenFileAsync(string title, params FileTypeFilter[] filters) => Task.FromResult(FileToOpen);

    public async Task<bool> SaveFileAsync(string title, string suggestedName, FileTypeFilter filter, Func<Stream, Task> write)
    {
        using var memory = new MemoryStream();
        await write(memory);
        SavedName = suggestedName;
        SavedBytes = memory.ToArray();
        return true;
    }
}

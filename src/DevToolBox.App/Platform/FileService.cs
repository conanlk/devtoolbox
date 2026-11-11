using Avalonia.Platform.Storage;

namespace DevToolBox.App.Platform;

/// <summary>A file chosen by the user (dialog or drag-and-drop), read on demand.</summary>
public sealed record PickedFile(string Name, long? Size, Func<Task<Stream>> OpenReadAsync)
{
    public static async Task<PickedFile> FromStorageFileAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);
        return new PickedFile(file.Name, (long?)properties.Size, file.OpenReadAsync);
    }
}

public sealed record FileTypeFilter(string Name, params string[] Extensions);

public interface IFileService
{
    Task<PickedFile?> OpenFileAsync(string title, params FileTypeFilter[] filters);

    /// <summary>Asks for a destination and writes it. Returns false when the user cancels.</summary>
    Task<bool> SaveFileAsync(string title, string suggestedName, FileTypeFilter filter, Func<Stream, Task> write);
}

public sealed class FileService(ITopLevelProvider topLevel) : IFileService
{
    public async Task<PickedFile?> OpenFileAsync(string title, params FileTypeFilter[] filters)
    {
        if (StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [.. filters.Select(ToPickerType), FilePickerFileTypes.All],
        }).ConfigureAwait(true);

        return files.Count > 0 ? await PickedFile.FromStorageFileAsync(files[0]).ConfigureAwait(true) : null;
    }

    public async Task<bool> SaveFileAsync(string title, string suggestedName, FileTypeFilter filter, Func<Stream, Task> write)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(write);
        if (StorageProvider is not { CanSave: true } storage)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = filter.Extensions.FirstOrDefault(),
            FileTypeChoices = [ToPickerType(filter)],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);
        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        await write(stream).ConfigureAwait(true);
        return true;
    }

    private IStorageProvider? StorageProvider => topLevel.TopLevel?.StorageProvider;

    private static FilePickerFileType ToPickerType(FileTypeFilter filter) =>
        new(filter.Name) { Patterns = [.. filter.Extensions.Select(e => "*." + e)] };
}

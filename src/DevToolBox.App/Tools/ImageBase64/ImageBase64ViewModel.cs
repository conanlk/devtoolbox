using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Tools.ImageBase64;

namespace DevToolBox.App.Tools.ImageBase64;

public enum Base64OutputKind
{
    Base64,
    DataUri,
    HtmlImg,
    CssUrl,
}

public sealed partial class ImageBase64ViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    /// <summary>Text boxes slow down with multi-megabyte strings; show a prefix, copy the full value.</summary>
    public const int MaxDisplayedChars = 100_000;


    private readonly IClipboardService _clipboard;
    private readonly IFileService _files;
    private readonly Debouncer _debouncer;
    private ImageEncoding? _encoding;
    private DecodedImage? _decoded;
    private (byte[] Bytes, string Name)? _source;

    public ImageBase64ViewModel(IClipboardService clipboard, IFileService files, TimeSpan? debounce = null)
    {
        _clipboard = clipboard;
        _files = files;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
        SelectedOutputKind = OutputKinds[1];
    }

    public IReadOnlyList<Choice<Base64OutputKind>> OutputKinds { get; } =
    [
        new("Base64", Base64OutputKind.Base64),
        new("Data URI", Base64OutputKind.DataUri),
        new(() => Strings.ImageBase64_ImgTag, Base64OutputKind.HtmlImg),
        new("CSS url()", Base64OutputKind.CssUrl),
    ];

    // ----- Image → Base64 -----

    [ObservableProperty]
    public partial Choice<Base64OutputKind> SelectedOutputKind { get; set; }

    [ObservableProperty]
    public partial Bitmap? SourcePreview { get; private set; }

    [ObservableProperty]
    public partial string? SourceInfo { get; private set; }

    /// <summary>The full output (copied to the clipboard).</summary>
    [ObservableProperty]
    public partial string Output { get; private set; } = string.Empty;

    /// <summary>What the text box shows (truncated for very large images).</summary>
    [ObservableProperty]
    public partial string OutputDisplay { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? EncodeWarning { get; private set; }

    [ObservableProperty]
    public partial string? EncodeError { get; private set; }

    // ----- Base64 → Image -----

    [ObservableProperty]
    public partial string Base64Input { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Bitmap? DecodedPreview { get; private set; }

    [ObservableProperty]
    public partial string? DecodedInfo { get; private set; }

    [ObservableProperty]
    public partial string? DecodeError { get; private set; }

    public bool HasDecodedImage => _decoded is not null;

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        SourcePreview?.Dispose();
        DecodedPreview?.Dispose();
    }

    /// <summary>Encodes image bytes (from a file, drop or clipboard).</summary>
    public void LoadImage(byte[] bytes, string name)
    {
        _source = (bytes, name);
        var result = ImageBase64Converter.Encode(bytes, name);
        _encoding = result.IsOk ? result.Value : null;
        EncodeError = result.Error?.Message;
        EncodeWarning = bytes.Length > ImageBase64Converter.WarnImageBytes
            ? Strings.ImageBase64_LargeWarning
            : null;
        ReplacePreview(p => SourcePreview = p, SourcePreview, TryDecodeBitmap(bytes));
        SourceInfo = _encoding is null
            ? name
            : Localizer.Format(Strings.ImageBase64_SourceInfo, name, _encoding.MimeType, FormatBytes(bytes.Length) + Dimensions(SourcePreview),
                FormatBytes(_encoding.Base64.Length));
        UpdateOutput();
    }

    /// <summary>Decodes the Base64 input immediately.</summary>
    public void DecodeInput()
    {
        _decoded = null;
        DecodeError = null;
        DecodedInfo = null;
        Bitmap? preview = null;
        if (!string.IsNullOrWhiteSpace(Base64Input))
        {
            var result = ImageBase64Converter.Decode(Base64Input);
            if (result.IsOk)
            {
                _decoded = result.Value;
                preview = TryDecodeBitmap(_decoded.Bytes);
                DecodedInfo = $"{_decoded.Format.MimeType} · {FormatBytes(_decoded.Bytes.Length)}{Dimensions(preview)}"
                              + (preview is null ? Strings.ImageBase64_NoPreview : string.Empty);
            }
            else
            {
                DecodeError = result.Error.Message;
            }
        }

        ReplacePreview(p => DecodedPreview = p, DecodedPreview, preview);
        OnPropertyChanged(nameof(HasDecodedImage));
    }

    [RelayCommand]
    private async Task OpenImageAsync() => await LoadFileAsync(await _files.OpenFileAsync(Strings.ImageBase64_ChooseImageTitle,
        new FileTypeFilter(Strings.ImageBase64_Images, "png", "jpg", "jpeg", "gif", "webp", "bmp", "ico", "svg", "avif", "tif", "tiff")));

    [RelayCommand]
    private async Task LoadFileAsync(PickedFile? file)
    {
        if (file is null)
        {
            return;
        }

        if (file.Size > ImageBase64Converter.MaxImageBytes)
        {
            EncodeError = Localizer.Format(Strings.ImageBase64_TooLarge, ImageBase64Converter.MaxImageBytes / (1024 * 1024));
            return;
        }

        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        LoadImage(memory.ToArray(), file.Name);
    }

    [RelayCommand]
    private async Task PasteImageAsync()
    {
        if (await _clipboard.GetBitmapAsync() is { } bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, PngBitmapEncoderOptions.Default);
            LoadImage(memory.ToArray(), Strings.ImageBase64_PastedImageName);
        }
        else if (await _clipboard.GetTextAsync() is { } text && text.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            Base64Input = text;
        }
        else
        {
            EncodeError = Strings.ImageBase64_NoClipboardImage;
        }
    }

    [RelayCommand]
    private async Task SaveDecodedAsync()
    {
        if (_decoded is { } image)
        {
            await _files.SaveFileAsync(Strings.ImageBase64_SaveImageTitle, $"image.{image.Format.Extension}",
                new FileTypeFilter(image.Format.MimeType, image.Format.Extension),
                stream => stream.WriteAsync(image.Bytes).AsTask());
        }
    }

    /// <summary>Re-describes the loaded and decoded images in the new language.</summary>
    protected override void OnLanguageChanged()
    {
        if (_source is { } source)
        {
            LoadImage(source.Bytes, source.Name);
        }

        DecodeInput();
    }

    partial void OnSelectedOutputKindChanged(Choice<Base64OutputKind> value) => UpdateOutput();

    partial void OnBase64InputChanged(string value) => _debouncer.Run(DecodeInput);

    private void UpdateOutput()
    {
        Output = _encoding is null || SelectedOutputKind is null
            ? string.Empty
            : SelectedOutputKind.Value switch
            {
                Base64OutputKind.Base64 => _encoding.Base64,
                Base64OutputKind.HtmlImg => _encoding.HtmlImgTag,
                Base64OutputKind.CssUrl => _encoding.CssUrl,
                _ => _encoding.DataUri,
            };
        OutputDisplay = Output.Length > MaxDisplayedChars
            ? Output[..MaxDisplayedChars] + "\n" + Localizer.Format(Strings.ImageBase64_Truncated, Output.Length - MaxDisplayedChars)
            : Output;
    }

    private static Bitmap? TryDecodeBitmap(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            // SVG/AVIF/TIFF (or a corrupt file): no preview, the Base64 is still valid.
            return null;
        }
    }

    private static void ReplacePreview(Action<Bitmap?> set, Bitmap? previous, Bitmap? next)
    {
        set(next);
        previous?.Dispose();
    }

    private static string Dimensions(Bitmap? bitmap) =>
        bitmap is null ? string.Empty : $" · {bitmap.PixelSize.Width}×{bitmap.PixelSize.Height}";

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.##} MB"),
    };

    /// <summary>0 = Image → Base64 tab, 1 = Base64 → Image tab.</summary>
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    public void AcceptInput(string text, string? intent)
    {
        SelectedTab = 1;
        Base64Input = text;
        DecodeInput();
    }
}

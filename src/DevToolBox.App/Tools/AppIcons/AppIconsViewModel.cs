using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Tools.AppIcons;
using DevToolBox.Core.Tools.ImageBase64;

namespace DevToolBox.App.Tools.AppIcons;

/// <summary>A row of the "Output files" table.</summary>
public sealed class OutputFileViewModel(PackageFile file, Bitmap? image, IAsyncRelayCommand<OutputFileViewModel> save)
{
    public PackageFile File { get; } = file;

    public IconSpec? Spec => File.Icon;

    /// <summary>The rendered PNG; null for text files or before an image is loaded.</summary>
    public Bitmap? Image { get; } = image;

    public bool IsImage => Spec is not null;

    public bool HasImage => Image is not null;

    /// <summary>Path below the platform root, e.g. "Icon-20@2x.png" or "mipmap-hdpi/ic_launcher.png".</summary>
    public string DisplayPath { get; } = DisplayPathOf(file.Path);

    public string FileName => System.IO.Path.GetFileName(File.Path);

    public string SizeText => Spec is null ? "—" : $"{Spec.PixelSize} × {Spec.PixelSize}";

    public string Use => AppIconLabels.Localize(File.Use);

    /// <summary>Screen-reader name of the row's save button.</summary>
    public string SaveAutomationName => Localizer.Format(Strings.AppIcons_SaveFile, FileName);

    /// <summary>Thumbnail corner rounding that hints at how the platform shows this file.</summary>
    public CornerRadius ThumbnailRadius => Spec?.Shape switch
    {
        IconShape.Round => new CornerRadius(12),
        IconShape.AdaptiveForeground => new CornerRadius(0),
        _ when File.Platform == IconPlatform.Ios => new CornerRadius(5),
        _ => new CornerRadius(4),
    };

    public IAsyncRelayCommand<OutputFileViewModel> SaveCommand { get; } = save;

    private static string DisplayPathOf(string path)
    {
        foreach (var root in new[] { AppIconCatalog.IosFolder + "/", AppIconCatalog.AndroidResFolder + "/", "android/" })
        {
            if (path.StartsWith(root, StringComparison.Ordinal))
            {
                return path[root.Length..];
            }
        }

        return path;
    }
}

/// <param name="Image">The loaded image under this mask, or the DevToolBox app icon before one is loaded.</param>
public sealed record MaskPreviewViewModel(string Label, Bitmap Image);

/// <summary>A launcher mask shown in the preview row.</summary>
internal sealed record MaskOption(Func<string> Label, IconMask Mask);

/// <summary>Home-screen wallpaper behind the mask previews.</summary>
public enum PreviewBackdrop
{
    Dark,
    Light,
}

public sealed record SourceCheck(string Text, bool IsWarning);

/// <summary>
/// App Icons: one 1024×1024 (or larger) image → the iOS set + Android legacy/round/adaptive icons,
/// previewed under launcher masks and exported as a project-ready ZIP.
/// </summary>
public sealed partial class AppIconsViewModel : ToolViewModelBase, IAcceptsInput, IDisposable
{
    private const string DefaultBackground = "#FFFFFF";

    /// <summary>Mask previews are drawn at 2× their 76 px display size for sharp HiDPI rendering.</summary>
    private const int MaskPreviewPixels = 152;

    private const int ThumbnailPixels = 176;

    private static readonly MaskOption[] Masks =
    [
        new(() => "iOS", IconMask.IosRoundedRect),
        new(() => Strings.AppIcons_MaskCircle, IconMask.AdaptiveCircle),
        new(() => Strings.AppIcons_MaskSquircle, IconMask.AdaptiveSquircle),
        new(() => Strings.AppIcons_MaskRoundedSquare, IconMask.AdaptiveRoundedSquare),
        new(() => Strings.AppIcons_MaskTeardrop, IconMask.AdaptiveTeardrop),
    ];

    // Home-screen wallpapers for the mask preview: they simulate a device, so they don't follow the app theme.
    private static readonly IBrush DarkWallpaper = new ImmutableSolidColorBrush(Color.Parse("#0A0C10"));
    private static readonly IBrush LightWallpaper = new ImmutableSolidColorBrush(Color.Parse("#E9ECF1"));
    private static readonly IBrush DarkWallpaperText = new ImmutableSolidColorBrush(Color.Parse("#C3C9D4"));
    private static readonly IBrush LightWallpaperText = new ImmutableSolidColorBrush(Color.Parse("#2A303C"));

    private readonly IFileService _files;
    private readonly IClipboardService _clipboard;
    private readonly Debouncer _debouncer;
    private Bitmap? _source;
    private IconSourcePlan? _plan;
    private bool _sourceHasTransparency;
    private Dictionary<string, Bitmap> _rendered = [];

    public AppIconsViewModel(IFileService files, IClipboardService clipboard, TimeSpan? debounce = null)
    {
        _files = files;
        _clipboard = clipboard;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
        MaskPreviews = DefaultMaskPreviews();
        RefreshFiles();
    }

    [ObservableProperty]
    public partial IconPlatform Platform { get; set; } = IconPlatform.Ios;

    [ObservableProperty]
    public partial bool IncludeIos { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeAndroid { get; set; } = true;

    [ObservableProperty]
    public partial bool AdaptiveIcon { get; set; } = true;

    [ObservableProperty]
    public partial bool RoundIcon { get; set; } = true;

    [ObservableProperty]
    public partial bool PlayStoreIcon { get; set; } = true;

    [ObservableProperty]
    public partial string BackgroundHex { get; set; } = DefaultBackground;

    [ObservableProperty]
    public partial IBrush BackgroundBrush { get; private set; } = Brushes.White;

    [ObservableProperty]
    public partial string? BackgroundError { get; private set; }

    /// <summary>Space around the artwork on each side, in percent of the artwork size.</summary>
    [ObservableProperty]
    public partial double Padding { get; set; }

    [ObservableProperty]
    public partial PreviewBackdrop Backdrop { get; set; }

    [ObservableProperty]
    public partial Bitmap? SourceThumbnail { get; private set; }

    [ObservableProperty]
    public partial string? SourceName { get; private set; }

    /// <summary>"1024 × 1024 · PNG · 184 KB".</summary>
    [ObservableProperty]
    public partial string? SourceInfo { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<SourceCheck> SourceChecks { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<MaskPreviewViewModel> MaskPreviews { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<OutputFileViewModel> IosFiles { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<OutputFileViewModel> AndroidFiles { get; private set; } = [];

    /// <summary>"16 files": what each platform checkbox adds, with the current Android extras.</summary>
    [ObservableProperty]
    public partial string IosFileCountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AndroidFileCountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ExportText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasIcons { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = Strings.AppIcons_EmptyStatus;

    public bool IsIos => Platform == IconPlatform.Ios;

    public bool IsAndroid => Platform == IconPlatform.Android;

    public bool HasWarnings => Warnings.Count > 0;

    public string IosTabText => $"iOS · {IosFiles.Count}";

    public string AndroidTabText => $"Android · {AndroidFiles.Count}";

    public IReadOnlyList<OutputFileViewModel> CurrentFiles => IsIos ? IosFiles : AndroidFiles;

    /// <summary>The selected tab's platform is switched off in Options, so its table is empty.</summary>
    public bool CurrentPlatformExcluded => IsIos ? !IncludeIos : !IncludeAndroid;

    public IBrush PreviewWallpaper => Backdrop == PreviewBackdrop.Light ? LightWallpaper : DarkWallpaper;

    public IBrush PreviewLabelBrush => Backdrop == PreviewBackdrop.Light ? LightWallpaperText : DarkWallpaperText;

    public AppIconOptions Options => new(IncludeIos, IncludeAndroid, AdaptiveIcon, RoundIcon, PlayStoreIcon);

    public bool CanExport => HasIcons && (IncludeIos || IncludeAndroid);

    /// <summary>The rendered PNG bytes of the selected icons by ZIP path (empty until an image is loaded).</summary>
    public IReadOnlyDictionary<string, byte[]> EncodeAll()
    {
        var options = Options;
        return AppIconCatalog.All
            .Where(spec => options.Includes(spec) && _rendered.ContainsKey(spec.Path))
            .ToDictionary(spec => spec.Path, spec => AppIconRenderer.ToPng(_rendered[spec.Path]), StringComparer.Ordinal);
    }

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        DisposeRendered();
        _source?.Dispose();
    }

    /// <summary>Loads image bytes (file, drop, paste or data URI) and renders every icon.</summary>
    public void LoadImage(byte[] bytes, string name)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        Bitmap? bitmap;
        try
        {
            using var stream = new MemoryStream(bytes);
            bitmap = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            Fail(Strings.AppIcons_UnreadableImage);
            return;
        }

        var size = bitmap.PixelSize;
        var hasTransparency = AppIconRenderer.HasTransparency(bitmap);
        var plan = AppIconSource.Plan(size.Width, size.Height, hasTransparency);
        if (!plan.IsOk)
        {
            bitmap.Dispose();
            Fail(plan.Error.Message);
            return;
        }

        _source?.Dispose();
        _source = bitmap;
        _plan = plan.Value;
        _sourceHasTransparency = hasTransparency;
        ErrorMessage = null;
        ShowSourceChecks(plan.Value);
        SourceName = name;
        var format = ImageFormats.Detect(bytes)?.Extension.ToUpperInvariant() ?? "Image";
        SourceInfo = string.Create(CultureInfo.InvariantCulture, $"{size.Width} × {size.Height} · {format} · {FormatBytes(bytes.Length)}");
        RenderAll();
    }

    private void ShowSourceChecks(IconSourcePlan plan)
    {
        var size = _source!.PixelSize;
        Warnings = plan.Warnings;
        OnPropertyChanged(nameof(HasWarnings));
        SourceChecks =
        [
            new SourceCheck(size.Width == size.Height ? Strings.AppIcons_CheckSquare : Strings.AppIcons_CheckSize, IsWarning: false),
            .. plan.Warnings.Select(w => new SourceCheck(w, IsWarning: true)),
        ];
    }

    /// <summary>Relabels checks, previews and the file table; rendered icons are reused, not redrawn.</summary>
    protected override void OnLanguageChanged()
    {
        if (_source is { } source && AppIconSource.Plan(source.PixelSize.Width, source.PixelSize.Height, _sourceHasTransparency) is { IsOk: true } plan)
        {
            ShowSourceChecks(plan.Value);
        }

        MaskPreviews = [.. MaskPreviews.Zip(Masks, (preview, mask) => preview with { Label = mask.Label() })];
        if (BackgroundError is not null)
        {
            BackgroundError = Strings.AppIcons_BackgroundError;
        }

        RefreshFiles();
    }

    public void AcceptInput(string text, string? intent)
    {
        var decoded = ImageBase64Converter.Decode(text);
        if (decoded.IsOk)
        {
            LoadImage(decoded.Value.Bytes, $"pasted.{decoded.Value.Format.Extension}");
        }
        else
        {
            Fail(decoded.Error.Message);
        }
    }

    /// <summary>Re-renders every icon and preview from the current source and options.</summary>
    public void RenderAll()
    {
        if (_source is null || _plan is null)
        {
            return;
        }

        var background = Background();
        var padding = Padding;

        // Keep the old bitmaps until the UI has the new ones, then release them.
        var oldBitmaps = _rendered.Values.Concat(PreviewBitmaps()).ToList();

        _rendered = AppIconCatalog.All.ToDictionary(
            spec => spec.Path,
            spec => AppIconRenderer.Render(_source, _plan, spec, background, padding),
            StringComparer.Ordinal);
        SourceThumbnail = AppIconRenderer.RenderThumbnail(_source, _plan, ThumbnailPixels);
        MaskPreviews =
        [
            .. Masks.Select(m => new MaskPreviewViewModel(
                m.Label(), AppIconRenderer.RenderPreview(_source, _plan, m.Mask, background, padding, MaskPreviewPixels))),
        ];
        HasIcons = true;
        RefreshFiles();

        foreach (var bitmap in oldBitmaps)
        {
            bitmap.Dispose();
        }
    }

    [RelayCommand]
    private async Task OpenImageAsync() => await LoadFileAsync(await _files.OpenFileAsync(Strings.AppIcons_ChooseImageTitle,
        new FileTypeFilter(Strings.ImageBase64_Images, "png", "jpg", "jpeg", "webp", "bmp")));

    [RelayCommand]
    private async Task LoadFileAsync(PickedFile? file)
    {
        if (file is null)
        {
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
            LoadImage(AppIconRenderer.ToPng(bitmap), "pasted.png");
        }
        else if (await _clipboard.GetTextAsync() is { } text && text.TrimStart().StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            AcceptInput(text, null);
        }
        else
        {
            Fail(Strings.ImageBase64_NoClipboardImage);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task DownloadZipAsync()
    {
        if (!CanExport)
        {
            return;
        }

        var images = EncodeAll();
        var hex = AppIconSource.NormalizeHexColor(BackgroundHex) ?? DefaultBackground;
        var options = Options;
        await _files.SaveFileAsync(Strings.AppIcons_SaveZipTitle, "AppIcons.zip", new FileTypeFilter(Strings.AppIcons_ZipArchive, "zip"), stream =>
        {
            AppIconPackage.WriteZip(stream, images, hex, options);
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task SaveIconAsync(OutputFileViewModel? file)
    {
        if (file?.Image is { } image && file.Spec is { } spec)
        {
            var png = AppIconRenderer.ToPng(image);
            var name = spec.Platform == IconPlatform.Android && spec.Path != AppIconCatalog.PlayStorePath
                ? $"{spec.Group}-{file.FileName}"
                : file.FileName;
            await _files.SaveFileAsync(Strings.AppIcons_SaveIconTitle, name, new FileTypeFilter(Strings.Common_PngImage, "png"),
                stream => stream.WriteAsync(png).AsTask());
        }
    }

    /// <summary>Clears the image and restores every option to its default.</summary>
    [RelayCommand]
    private void Reset()
    {
        // Rows and previews still show the old bitmaps until they're rebuilt below.
        var oldBitmaps = _rendered.Values.Concat(PreviewBitmaps()).ToList();
        var oldSource = _source;
        _rendered = [];
        SourceThumbnail = null;
        _source = null;
        _plan = null;
        HasIcons = false;
        SourceName = null;
        SourceInfo = null;
        SourceChecks = [];
        Warnings = [];
        OnPropertyChanged(nameof(HasWarnings));
        ErrorMessage = null;
        MaskPreviews = DefaultMaskPreviews();
        IncludeIos = IncludeAndroid = AdaptiveIcon = RoundIcon = PlayStoreIcon = true;
        BackgroundHex = DefaultBackground;
        Padding = 0;
        Backdrop = PreviewBackdrop.Dark;
        Platform = IconPlatform.Ios;
        RefreshFiles();

        foreach (var bitmap in oldBitmaps)
        {
            bitmap.Dispose();
        }

        oldSource?.Dispose();
    }

    partial void OnPlatformChanged(IconPlatform value)
    {
        OnPropertyChanged(nameof(IsIos));
        OnPropertyChanged(nameof(IsAndroid));
        OnPropertyChanged(nameof(CurrentFiles));
        OnPropertyChanged(nameof(CurrentPlatformExcluded));
    }

    partial void OnIncludeIosChanged(bool value) => RefreshFiles();

    partial void OnIncludeAndroidChanged(bool value) => RefreshFiles();

    partial void OnAdaptiveIconChanged(bool value) => RefreshFiles();

    partial void OnRoundIconChanged(bool value) => RefreshFiles();

    partial void OnPlayStoreIconChanged(bool value) => RefreshFiles();

    partial void OnBackdropChanged(PreviewBackdrop value)
    {
        OnPropertyChanged(nameof(PreviewWallpaper));
        OnPropertyChanged(nameof(PreviewLabelBrush));
    }

    partial void OnBackgroundHexChanged(string value)
    {
        var hex = AppIconSource.NormalizeHexColor(value);
        BackgroundError = hex is null ? Strings.AppIcons_BackgroundError : null;
        if (hex is not null)
        {
            BackgroundBrush = new ImmutableSolidColorBrush(Color.Parse(hex));
            _debouncer.Run(RenderAll);
        }
    }

    partial void OnPaddingChanged(double value) => _debouncer.Run(RenderAll);

    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB")
        : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (bytes + 512) / 1024)} KB");

    /// <summary>Rebuilds the output table rows, counts and export label from the current options.</summary>
    private void RefreshFiles()
    {
        var options = Options;
        var save = SaveIconCommand;
        IReadOnlyList<OutputFileViewModel> Rows(IconPlatform platform) =>
        [
            .. AppIconPackage.Files(options)
                .Where(f => f.Platform == platform)
                .Select(f => new OutputFileViewModel(f, f.Icon is null ? null : _rendered.GetValueOrDefault(f.Icon.Path), save)),
        ];

        IosFiles = Rows(IconPlatform.Ios);
        AndroidFiles = Rows(IconPlatform.Android);
        OnPropertyChanged(nameof(IosTabText));
        OnPropertyChanged(nameof(AndroidTabText));
        OnPropertyChanged(nameof(CurrentFiles));
        OnPropertyChanged(nameof(CurrentPlatformExcluded));

        var everyPlatform = AppIconPackage.Files(options with { Ios = true, Android = true });
        IosFileCountText = Localizer.Format(Strings.AppIcons_FileCount, everyPlatform.Count(f => f.Platform == IconPlatform.Ios));
        AndroidFileCountText = Localizer.Format(Strings.AppIcons_FileCount, everyPlatform.Count(f => f.Platform == IconPlatform.Android));
        var total = IosFiles.Count + AndroidFiles.Count;
        ExportText = Localizer.Format(Strings.AppIcons_ExportZip, total);

        OnPropertyChanged(nameof(CanExport));
        DownloadZipCommand.NotifyCanExecuteChanged();
        StatusText = !HasIcons ? Strings.AppIcons_EmptyStatus
            : total == 0 ? Strings.AppIcons_SelectPlatform
            : "→ " + string.Join(" · ", new[]
            {
                IncludeIos ? AppIconCatalog.IosFolder : null,
                IncludeAndroid ? AppIconCatalog.AndroidResFolder : null,
            }.OfType<string>());
    }

    /// <summary>The app's own icon under each mask, so the row shows what it does before an image is loaded.</summary>
    private static List<MaskPreviewViewModel> DefaultMaskPreviews() =>
        [.. Masks.Select(m => new MaskPreviewViewModel(m.Label(), AppIconRenderer.RenderDefaultPreview(m.Mask, MaskPreviewPixels)))];

    private Color Background() => Color.Parse(AppIconSource.NormalizeHexColor(BackgroundHex) ?? DefaultBackground);

    private IEnumerable<Bitmap> PreviewBitmaps() =>
        MaskPreviews.Select(p => p.Image).Append(SourceThumbnail).OfType<Bitmap>();

    private void Fail(string message)
    {
        ErrorMessage = message;
        if (_source is null)
        {
            HasIcons = false;
            StatusText = Strings.AppIcons_EmptyStatus;
        }
    }

    private void DisposeRendered()
    {
        var bitmaps = _rendered.Values.Concat(PreviewBitmaps()).ToList();
        _rendered = [];
        SourceThumbnail = null;
        foreach (var bitmap in bitmaps)
        {
            bitmap.Dispose();
        }
    }
}

using System.IO.Compression;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools.AppIcons;
using DevToolBox.Core.Tools.AppIcons;

namespace DevToolBox.App.Tests.Tools;

/// <summary>Renders real images with Skia and checks the produced icons pixel by pixel.</summary>
public class AppIconsViewModelTests
{
    private readonly FakeFileService _files = new();
    private readonly FakeClipboardService _clipboard = new();

    /// <summary>A red disc on a transparent canvas: exercises crop + transparency handling.</summary>
    private static byte[] TestImage(int width, int height, bool transparent)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext(true))
        {
            if (!transparent)
            {
                context.FillRectangle(Brushes.Navy, new Rect(0, 0, width, height));
            }

            var side = Math.Min(width, height);
            context.DrawEllipse(Brushes.Red, null, new Rect((width - side) / 2.0, (height - side) / 2.0, side, side));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private static Color PixelAt(Bitmap bitmap, int x, int y)
    {
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), buffer, 4, 4);
            var bytes = new byte[4];
            Marshal.Copy(buffer, bytes, 0, 4);
            return bitmap.Format == Avalonia.Platform.PixelFormats.Rgba8888
                ? Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2])
                : Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private AppIconsViewModel Load(int width, int height, bool transparent, string background = "#FFFFFF")
    {
        var vm = new AppIconsViewModel(_files, _clipboard, TimeSpan.FromHours(1)) { BackgroundHex = background };
        vm.LoadImage(TestImage(width, height, transparent), "icon.png");
        return vm;
    }

    private static OutputFileViewModel Icon(AppIconsViewModel vm, string path) =>
        vm.IosFiles.Concat(vm.AndroidFiles).Single(f => f.File.Path == path);

    private static Bitmap Image(AppIconsViewModel vm, string path) => Icon(vm, path).Image!;

    private static async Task<ZipArchive> ExportAsync(AppIconsViewModel vm, FakeFileService files)
    {
        await vm.DownloadZipCommand.ExecuteAsync(null);
        return new ZipArchive(new MemoryStream(files.SavedBytes!));
    }

    /// <summary>Before an image is loaded each mask shows the app icon: graphite ground, amber cursor, masked corners.</summary>
    private static void AssertShowsAppIcon(AppIconsViewModel vm)
    {
        Assert.Equal(["iOS", "Android circle", "Squircle", "Rounded square", "Teardrop"], vm.MaskPreviews.Select(m => m.Label));
        Assert.All(vm.MaskPreviews, m =>
        {
            Assert.Equal(new PixelSize(152, 152), m.Image.PixelSize);
            Assert.Equal(0, PixelAt(m.Image, 1, 1).A);
            Assert.Equal(Color.Parse("#161A22"), PixelAt(m.Image, 76, 12));
            Assert.Equal(Color.Parse("#F2B544"), PixelAt(m.Image, 76, 76));
        });
    }

    [AvaloniaFact]
    public void New_PreviewsTheAppIconUnderEveryMask()
    {
        using var vm = new AppIconsViewModel(_files, _clipboard);

        Assert.False(vm.HasIcons);
        AssertShowsAppIcon(vm);
    }

    [AvaloniaFact]
    public void Load_RendersEveryIconAtItsExactSize()
    {
        using var vm = Load(1024, 1024, transparent: false);

        Assert.True(vm.HasIcons);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasWarnings);
        Assert.Equal(16, vm.IosFiles.Count);
        Assert.Equal(19, vm.AndroidFiles.Count);
        Assert.Equal("iOS · 16", vm.IosTabText);
        Assert.Equal("Export ZIP · 35 files", vm.ExportText);
        Assert.All(vm.IosFiles.Concat(vm.AndroidFiles).Where(f => f.IsImage),
            icon => Assert.Equal(new PixelSize(icon.Spec!.PixelSize, icon.Spec.PixelSize), icon.Image!.PixelSize));
        Assert.Equal(["iOS", "Android circle", "Squircle", "Rounded square", "Teardrop"], vm.MaskPreviews.Select(m => m.Label));
        Assert.All(vm.MaskPreviews, m => Assert.NotNull(m.Image));
        Assert.NotNull(vm.SourceThumbnail);
        Assert.StartsWith("1024 × 1024 · PNG · ", vm.SourceInfo, StringComparison.Ordinal);
        Assert.Equal([new SourceCheck("Square, at least 1024 px", false)], vm.SourceChecks);
        Assert.Equal("→ ios/AppIcon.appiconset · android/res", vm.StatusText);
    }

    [AvaloniaFact]
    public void TransparentNonSquare_IsCroppedAndFilled_ForIos_ButRoundStaysTransparentOutside()
    {
        using var vm = Load(1400, 1100, transparent: true, background: "#00FF00");

        Assert.Equal(2, vm.Warnings.Count);
        // Corner of the iOS App Store icon: outside the red disc → filled with the background colour, opaque.
        var iosCorner = PixelAt(Image(vm, "ios/AppIcon.appiconset/Icon-1024@1x.png"), 2, 2);
        Assert.Equal(255, iosCorner.A);
        Assert.True(iosCorner.G > 200 && iosCorner.R < 50);
        // Centre is the red artwork.
        var centre = PixelAt(Image(vm, "ios/AppIcon.appiconset/Icon-1024@1x.png"), 512, 512);
        Assert.True(centre.R > 200 && centre.G < 50);
        // Round launcher: corners are transparent.
        Assert.Equal(0, PixelAt(Image(vm, "android/res/mipmap-xxxhdpi/ic_launcher_round.png"), 1, 1).A);
        // Adaptive foreground: transparent around the scaled artwork.
        Assert.Equal(0, PixelAt(Image(vm, "android/res/mipmap-xxxhdpi/ic_launcher_foreground.png"), 5, 5).A);
        Assert.Contains(vm.SourceChecks, c => c.IsWarning);
    }

    [AvaloniaFact]
    public void TooSmallImage_IsRejected()
    {
        using var vm = Load(800, 800, transparent: false);

        Assert.False(vm.HasIcons);
        Assert.Contains("1024×1024", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void NotAnImage_IsRejected()
    {
        using var vm = new AppIconsViewModel(_files, _clipboard, TimeSpan.FromHours(1));

        vm.LoadImage("not an image"u8.ToArray(), "notes.txt");

        Assert.False(vm.HasIcons);
        Assert.NotNull(vm.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task DownloadZip_WritesProjectReadyArchive()
    {
        using var vm = Load(1024, 1024, transparent: false, background: "#0e1014");

        using var zip = await ExportAsync(vm, _files);

        Assert.Equal("AppIcons.zip", _files.SavedName);
        Assert.Equal(36, zip.Entries.Count);
        using var reader = new StreamReader(zip.GetEntry("android/res/values/ic_launcher_background.xml")!.Open());
        Assert.Contains("#0E1014", await reader.ReadToEndAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var png = zip.GetEntry("ios/AppIcon.appiconset/Icon-60@3x.png")!;
        using var pngStream = new MemoryStream();
        await png.Open().CopyToAsync(pngStream, TestContext.Current.CancellationToken);
        pngStream.Position = 0;
        using var decoded = new Bitmap(pngStream);
        Assert.Equal(new PixelSize(180, 180), decoded.PixelSize);
    }

    [AvaloniaFact]
    public async Task SaveIcon_SavesSinglePngWithDensityInName()
    {
        using var vm = Load(1024, 1024, transparent: false);

        await vm.SaveIconCommand.ExecuteAsync(Icon(vm, "android/res/mipmap-hdpi/ic_launcher.png"));

        Assert.Equal("hdpi-ic_launcher.png", _files.SavedName);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, _files.SavedBytes![..4]);
    }

    [AvaloniaFact]
    public void InvalidBackground_ShowsError_AndKeepsIcons()
    {
        using var vm = Load(1024, 1024, transparent: false);

        vm.BackgroundHex = "not-a-colour";

        Assert.NotNull(vm.BackgroundError);
        Assert.True(vm.HasIcons);
    }

    [AvaloniaFact]
    public void AcceptInput_DataUri_LoadsImage()
    {
        using var vm = new AppIconsViewModel(_files, _clipboard, TimeSpan.FromHours(1));

        vm.AcceptInput("data:image/png;base64," + Convert.ToBase64String(TestImage(1024, 1024, transparent: false)), null);

        Assert.True(vm.HasIcons);
        Assert.Equal("pasted.png", vm.SourceName);
        Assert.StartsWith("1024 × 1024 · PNG", vm.SourceInfo, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void PlatformTabs_Switch()
    {
        using var vm = Load(1024, 1024, transparent: false);

        vm.Platform = IconPlatform.Android;

        Assert.True(vm.IsAndroid);
        Assert.False(vm.IsIos);
    }

    [AvaloniaFact]
    public async Task Options_FilterTableAndZip()
    {
        using var vm = Load(1024, 1024, transparent: false);

        vm.IncludeIos = false;
        vm.RoundIcon = false;
        vm.PlayStoreIcon = false;

        Assert.Empty(vm.IosFiles);
        Assert.Equal("16 files", vm.IosFileCountText);
        Assert.Equal(12, vm.AndroidFiles.Count);
        Assert.DoesNotContain(vm.AndroidFiles, f => f.File.Path.Contains("_round", StringComparison.Ordinal));
        Assert.Equal("Export ZIP · 12 files", vm.ExportText);
        using var zip = await ExportAsync(vm, _files);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Equal(13, names.Count);
        Assert.DoesNotContain(names, n => n.StartsWith("ios/", StringComparison.Ordinal));
        Assert.DoesNotContain(AppIconCatalog.PlayStorePath, names);
    }

    [AvaloniaFact]
    public void NoPlatform_DisablesExport()
    {
        using var vm = Load(1024, 1024, transparent: false);

        vm.IncludeIos = false;
        vm.IncludeAndroid = false;

        Assert.False(vm.DownloadZipCommand.CanExecute(null));
        Assert.Equal("Select at least one platform to export", vm.StatusText);
    }

    [AvaloniaFact]
    public void Padding_ShrinksArtworkOntoBackground()
    {
        using var vm = Load(1024, 1024, transparent: true, background: "#00FF00");
        var path = "ios/AppIcon.appiconset/Icon-1024@1x.png";
        // The red disc touches the edge midpoints without padding.
        Assert.True(PixelAt(Image(vm, path), 512, 3).R > 200);

        vm.Padding = 20;
        vm.RenderAll();

        var edge = PixelAt(Image(vm, path), 512, 3);
        Assert.True(edge.G > 200 && edge.R < 50);
        Assert.True(PixelAt(Image(vm, path), 512, 512).R > 200);
    }

    [AvaloniaFact]
    public void Reset_ClearsImageAndRestoresDefaults()
    {
        using var vm = Load(1024, 1024, transparent: false, background: "#123456");
        vm.Padding = 10;
        vm.IncludeIos = false;
        vm.Backdrop = PreviewBackdrop.Light;

        vm.ResetCommand.Execute(null);

        Assert.False(vm.HasIcons);
        Assert.Null(vm.SourceThumbnail);
        AssertShowsAppIcon(vm);
        Assert.Equal("#FFFFFF", vm.BackgroundHex);
        Assert.Equal(0, vm.Padding);
        Assert.True(vm.IncludeIos);
        Assert.Equal(PreviewBackdrop.Dark, vm.Backdrop);
        Assert.Equal(16, vm.IosFiles.Count);
        Assert.All(vm.IosFiles, f => Assert.Null(f.Image));
        Assert.False(vm.DownloadZipCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void BeforeLoad_TableListsFilesWithoutImages()
    {
        using var vm = new AppIconsViewModel(_files, _clipboard, TimeSpan.FromHours(1));

        Assert.Equal("Icon-20@2x.png", vm.IosFiles[0].DisplayPath);
        Assert.Equal("40 × 40", vm.IosFiles[0].SizeText);
        Assert.Contains(vm.AndroidFiles, f => f.DisplayPath == "mipmap-anydpi-v26/ic_launcher.xml" && f.SizeText == "—");
        Assert.All(vm.IosFiles, f => Assert.False(f.HasImage));
    }
}

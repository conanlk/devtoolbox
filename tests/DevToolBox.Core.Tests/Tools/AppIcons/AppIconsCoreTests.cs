using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using DevToolBox.Core.Tools.AppIcons;

namespace DevToolBox.Core.Tests.Tools.AppIcons;

public class AppIconCatalogTests
{
    [Fact]
    public void Ios_HasEveryUniqueSizeOnce()
    {
        Assert.Equal(15, AppIconCatalog.Ios.Count);
        Assert.Equal(new[] { 20, 29, 40, 58, 60, 76, 80, 87, 120, 152, 167, 180, 1024 },
            AppIconCatalog.Ios.Select(i => i.PixelSize).Distinct().Order());
        Assert.Contains(AppIconCatalog.Ios, i => i.Path == "ios/AppIcon.appiconset/Icon-60@3x.png" && i.PixelSize == 180);
        Assert.Contains(AppIconCatalog.Ios, i => i.Path == "ios/AppIcon.appiconset/Icon-83.5@2x.png" && i.PixelSize == 167);
        Assert.All(AppIconCatalog.Ios, i => Assert.Equal(IconShape.Square, i.Shape));
    }

    [Fact]
    public void IosSlots_CoverIphoneIpadAndAppStore_AndReferenceRealFiles()
    {
        Assert.Equal(18, AppIconCatalog.IosSlots.Count);
        Assert.Equal(8, AppIconCatalog.IosSlots.Count(s => s.Idiom == "iphone"));
        Assert.Equal(9, AppIconCatalog.IosSlots.Count(s => s.Idiom == "ipad"));
        var files = AppIconCatalog.Ios.Select(i => Path.GetFileName(i.Path)).ToHashSet();
        Assert.All(AppIconCatalog.IosSlots, s => Assert.Contains(s.Filename, files));
    }

    [Theory]
    [InlineData("mdpi", 48, 108)]
    [InlineData("hdpi", 72, 162)]
    [InlineData("xhdpi", 96, 216)]
    [InlineData("xxhdpi", 144, 324)]
    [InlineData("xxxhdpi", 192, 432)]
    public void Android_HasLauncherRoundAndAdaptivePerDensity(string density, int launcher, int adaptive)
    {
        var specs = AppIconCatalog.Android.Where(a => a.Group == density).ToList();

        Assert.Equal(3, specs.Count);
        Assert.Equal(launcher, specs.Single(s => s.Path.EndsWith("/ic_launcher.png", StringComparison.Ordinal)).PixelSize);
        Assert.Equal(IconShape.Round, specs.Single(s => s.Path.EndsWith("_round.png", StringComparison.Ordinal)).Shape);
        Assert.Equal(adaptive, specs.Single(s => s.Shape == IconShape.AdaptiveForeground).PixelSize);
        Assert.All(specs, s => Assert.StartsWith($"android/res/mipmap-{density}/", s.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Android_IncludesPlayStoreIcon_AndAllPathsAreUnique()
    {
        Assert.Equal(16, AppIconCatalog.Android.Count);
        Assert.Contains(AppIconCatalog.Android, a => a.Path == "android/play-store-512.png" && a.PixelSize == 512);
        Assert.Equal(AppIconCatalog.All.Count, AppIconCatalog.All.Select(a => a.Path).Distinct().Count());
    }
}

public class AppIconPackageTests
{
    [Fact]
    public void ContentsJson_IsValidXcodeAssetCatalog()
    {
        using var doc = JsonDocument.Parse(AppIconPackage.IosContentsJson());

        var images = doc.RootElement.GetProperty("images").EnumerateArray().ToList();
        Assert.Equal(18, images.Count);
        Assert.Contains(images, i => i.GetProperty("idiom").GetString() == "ios-marketing" && i.GetProperty("size").GetString() == "1024x1024");
        Assert.Contains(images, i => i.GetProperty("size").GetString() == "83.5x83.5" && i.GetProperty("scale").GetString() == "2x");
        Assert.Equal("xcode", doc.RootElement.GetProperty("info").GetProperty("author").GetString());
    }

    [Fact]
    public void AndroidXml_IsWellFormed()
    {
        var adaptive = XDocument.Parse(AppIconPackage.AndroidAdaptiveIconXml());
        var color = XDocument.Parse(AppIconPackage.AndroidBackgroundColorXml("#0E1014"));

        Assert.Equal("adaptive-icon", adaptive.Root!.Name.LocalName);
        Assert.Equal(2, adaptive.Root.Elements().Count());
        Assert.Equal("#0E1014", color.Root!.Element("color")!.Value);
    }

    [Fact]
    public void WriteZip_ContainsImagesAndTextFilesInProjectLayout()
    {
        var images = AppIconCatalog.All.ToDictionary(s => s.Path, _ => new byte[] { 1, 2, 3 });
        using var stream = new MemoryStream();

        AppIconPackage.WriteZip(stream, images, "#FFFFFF");

        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Equal(31 + 5, names.Count);
        Assert.Contains("ios/AppIcon.appiconset/Contents.json", names);
        Assert.Contains("android/res/mipmap-anydpi-v26/ic_launcher.xml", names);
        Assert.Contains("android/res/values/ic_launcher_background.xml", names);
        Assert.Contains("README.txt", names);
        Assert.Contains("android/res/mipmap-xxxhdpi/ic_launcher_foreground.png", names);
    }
}

public class AppIconOptionsTests
{
    [Fact]
    public void Files_DefaultOptions_ListEveryFileExceptReadme()
    {
        var files = AppIconPackage.Files();

        Assert.Equal(16, files.Count(f => f.Platform == IconPlatform.Ios));
        Assert.Equal(19, files.Count(f => f.Platform == IconPlatform.Android));
        Assert.Equal("ios/AppIcon.appiconset/Contents.json", files.Last(f => f.Platform == IconPlatform.Ios).Path);
        Assert.Equal(AppIconCatalog.PlayStorePath, files[^1].Path);
    }

    [Fact]
    public void Files_WithoutAdaptiveOrRound_DropTheirImagesAndXml()
    {
        var files = AppIconPackage.Files(new AppIconOptions(Ios: false, AdaptiveIcon: false, RoundIcon: false));

        Assert.DoesNotContain(files, f => f.Platform == IconPlatform.Ios);
        Assert.Equal(6, files.Count);
        Assert.All(files, f => Assert.Equal(IconShape.Square, f.Icon!.Shape));
    }

    [Fact]
    public void TextFiles_FollowOptions()
    {
        var texts = AppIconPackage.TextFiles("#FFFFFF", new AppIconOptions(Android: false));

        Assert.Equal(["README.txt", "ios/AppIcon.appiconset/Contents.json"], texts.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("android/", texts["README.txt"], StringComparison.Ordinal);
    }

    [Fact]
    public void TextFiles_AdaptiveWithoutRound_SkipsRoundXml()
    {
        var texts = AppIconPackage.TextFiles("#FFFFFF", new AppIconOptions(RoundIcon: false));

        Assert.Contains("android/res/mipmap-anydpi-v26/ic_launcher.xml", texts.Keys);
        Assert.DoesNotContain("android/res/mipmap-anydpi-v26/ic_launcher_round.xml", texts.Keys);
        Assert.DoesNotContain("roundIcon", texts["README.txt"], StringComparison.Ordinal);
    }
}

public class AppIconSourceTests
{
    [Fact]
    public void SquareOpaque_IsUsedAsIs()
    {
        var plan = AppIconSource.Plan(1024, 1024, hasTransparency: false).Value;

        Assert.Equal((0, 0, 1024), (plan.CropX, plan.CropY, plan.CropSize));
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void NonSquare_IsCentreCropped_WithWarning()
    {
        var plan = AppIconSource.Plan(2048, 1200, hasTransparency: true).Value;

        Assert.Equal((424, 0, 1200), (plan.CropX, plan.CropY, plan.CropSize));
        Assert.Equal(2, plan.Warnings.Count);
        Assert.Contains("not square", plan.Warnings[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(512, 512)]
    [InlineData(2048, 1000)]
    public void TooSmall_IsRejected(int width, int height) =>
        Assert.Equal("appicons.too-small", AppIconSource.Plan(width, height, false).Error?.Code);

    [Theory]
    [InlineData("#fff", "#FFFFFF")]
    [InlineData("0e1014", "#0E1014")]
    [InlineData(" #F2B544 ", "#F2B544")]
    [InlineData("#12345", null)]
    [InlineData("red", null)]
    public void NormalizeHexColor(string input, string? expected) => Assert.Equal(expected, AppIconSource.NormalizeHexColor(input));
}

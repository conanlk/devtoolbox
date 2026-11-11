using System.Globalization;

namespace DevToolBox.Core.Tools.AppIcons;

public enum IconPlatform
{
    Ios,
    Android,
}

/// <summary>How an icon image is drawn from the source.</summary>
public enum IconShape
{
    /// <summary>Full square, opaque (transparency filled with the background colour).</summary>
    Square,

    /// <summary>Circle on a transparent canvas (Android ic_launcher_round).</summary>
    Round,

    /// <summary>Android adaptive-icon foreground: transparent canvas, artwork scaled into the safe zone.</summary>
    AdaptiveForeground,
}

/// <param name="Group">Section in the preview, e.g. "iPhone", "iPad", "App Store", "xxhdpi".</param>
/// <param name="Label">Human description, e.g. "App · 60pt @3x".</param>
/// <param name="Path">Path inside the downloaded ZIP.</param>
public sealed record IconSpec(IconPlatform Platform, string Group, string Label, string Path, int PixelSize, IconShape Shape);

/// <summary>An image slot in Xcode's AppIcon.appiconset/Contents.json.</summary>
public sealed record IosIconSlot(string Idiom, string PointSize, string Scale, string Filename, string Role);

/// <summary>
/// Every icon a mobile app needs. iOS: the full iPhone + iPad + App Store set, so it works with any Xcode
/// and React Native version. Android: legacy square, round and adaptive foreground per density, plus the
/// 512 px Play Store icon.
/// </summary>
public static class AppIconCatalog
{
    public const int MinimumSourceSize = 1024;
    public const string IosFolder = "ios/AppIcon.appiconset";
    public const string AndroidResFolder = "android/res";
    public const string PlayStorePath = "android/play-store-512.png";

    /// <summary>iPhone/iPad/marketing slots (Xcode "All sizes" layout). Several slots share one file.</summary>
    public static IReadOnlyList<IosIconSlot> IosSlots { get; } =
    [
        Slot("iphone", "20", 2, "Notification"), Slot("iphone", "20", 3, "Notification"),
        Slot("iphone", "29", 2, "Settings"), Slot("iphone", "29", 3, "Settings"),
        Slot("iphone", "40", 2, "Spotlight"), Slot("iphone", "40", 3, "Spotlight"),
        Slot("iphone", "60", 2, "App"), Slot("iphone", "60", 3, "App"),
        Slot("ipad", "20", 1, "Notification"), Slot("ipad", "20", 2, "Notification"),
        Slot("ipad", "29", 1, "Settings"), Slot("ipad", "29", 2, "Settings"),
        Slot("ipad", "40", 1, "Spotlight"), Slot("ipad", "40", 2, "Spotlight"),
        Slot("ipad", "76", 1, "App"), Slot("ipad", "76", 2, "App"),
        Slot("ipad", "83.5", 2, "App (iPad Pro)"),
        Slot("ios-marketing", "1024", 1, "App Store"),
    ];

    /// <summary>Android density buckets and their scale relative to mdpi.</summary>
    public static IReadOnlyList<(string Density, double Scale)> AndroidDensities { get; } =
    [
        ("mdpi", 1.0), ("hdpi", 1.5), ("xhdpi", 2.0), ("xxhdpi", 3.0), ("xxxhdpi", 4.0),
    ];

    /// <summary>One file per unique iOS pixel image, ordered iPhone → iPad → App Store.</summary>
    public static IReadOnlyList<IconSpec> Ios { get; } = BuildIos();

    public static IReadOnlyList<IconSpec> Android { get; } = BuildAndroid();

    public static IReadOnlyList<IconSpec> All { get; } = [.. Ios, .. Android];

    public static int PixelSize(string pointSize, string scale) =>
        (int)Math.Round(double.Parse(pointSize, CultureInfo.InvariantCulture) * int.Parse(scale.TrimEnd('x'), CultureInfo.InvariantCulture));

    private static IosIconSlot Slot(string idiom, string points, int scale, string role) =>
        new(idiom, points, $"{scale}x", $"Icon-{points}@{scale}x.png", role);

    private static List<IconSpec> BuildIos()
    {
        var specs = new List<IconSpec>();
        foreach (var slot in IosSlots.DistinctBy(s => s.Filename))
        {
            var group = slot.Idiom switch
            {
                "iphone" => "iPhone",
                "ipad" => "iPad",
                _ => "App Store",
            };
            specs.Add(new IconSpec(IconPlatform.Ios, group, $"{slot.Role} · {slot.PointSize}pt @{slot.Scale}",
                $"{IosFolder}/{slot.Filename}", PixelSize(slot.PointSize, slot.Scale), IconShape.Square));
        }

        return specs;
    }

    private static List<IconSpec> BuildAndroid()
    {
        var specs = new List<IconSpec>();
        foreach (var (density, scale) in AndroidDensities)
        {
            var folder = $"{AndroidResFolder}/mipmap-{density}";
            var launcher = (int)Math.Round(48 * scale);
            var adaptive = (int)Math.Round(108 * scale);
            specs.Add(new IconSpec(IconPlatform.Android, density, "Launcher", $"{folder}/ic_launcher.png", launcher, IconShape.Square));
            specs.Add(new IconSpec(IconPlatform.Android, density, "Launcher (round)", $"{folder}/ic_launcher_round.png", launcher, IconShape.Round));
            specs.Add(new IconSpec(IconPlatform.Android, density, "Adaptive foreground", $"{folder}/ic_launcher_foreground.png", adaptive, IconShape.AdaptiveForeground));
        }

        specs.Add(new IconSpec(IconPlatform.Android, "Play Store", "Play Store listing", PlayStorePath, 512, IconShape.Square));
        return specs;
    }
}

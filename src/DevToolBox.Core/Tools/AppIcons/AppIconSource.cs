using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.AppIcons;

/// <summary>The square region of the source image used for icons, and what was adjusted to get it.</summary>
public sealed record IconSourcePlan(int CropX, int CropY, int CropSize, IReadOnlyList<string> Warnings);

public static class AppIconSource
{
    /// <summary>
    /// Accepts images whose shorter side is at least 1024 px. Non-square images are centre-cropped; transparent
    /// images get a warning because iOS icons (and the App Store) must be opaque, so they're filled.
    /// </summary>
    public static Result<IconSourcePlan> Plan(int width, int height, bool hasTransparency)
    {
        var shortest = Math.Min(width, height);
        if (shortest < AppIconCatalog.MinimumSourceSize)
        {
            return new Error("appicons.too-small",
                CoreLocalization.Format(CoreStrings.AppIcons_TooSmall, width, height, AppIconCatalog.MinimumSourceSize));
        }

        var warnings = new List<string>();
        if (width != height)
        {
            warnings.Add(CoreLocalization.Format(CoreStrings.AppIcons_NotSquare, width, height, shortest));
        }

        if (hasTransparency)
        {
            warnings.Add(CoreStrings.AppIcons_Transparent);
        }

        return new IconSourcePlan((width - shortest) / 2, (height - shortest) / 2, shortest, warnings);
    }

    /// <summary>Normalizes "#abc", "abc", "#AABBCC" to "#AABBCC"; null when invalid.</summary>
    public static string? NormalizeHexColor(string? input)
    {
        var hex = (input ?? string.Empty).Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        return hex.Length == 6 && hex.All(char.IsAsciiHexDigit) ? "#" + hex.ToUpperInvariant() : null;
    }
}

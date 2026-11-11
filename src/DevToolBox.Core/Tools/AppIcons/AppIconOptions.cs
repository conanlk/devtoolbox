namespace DevToolBox.Core.Tools.AppIcons;

/// <summary>Which platforms and optional Android icons go into the package.</summary>
public sealed record AppIconOptions(
    bool Ios = true,
    bool Android = true,
    bool AdaptiveIcon = true,
    bool RoundIcon = true,
    bool PlayStoreIcon = true)
{
    public static AppIconOptions All { get; } = new();

    public bool Includes(IconSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Platform == IconPlatform.Ios)
        {
            return Ios;
        }

        return Android && spec.Shape switch
        {
            IconShape.Round => RoundIcon,
            IconShape.AdaptiveForeground => AdaptiveIcon,
            _ => spec.Path != AppIconCatalog.PlayStorePath || PlayStoreIcon,
        };
    }
}

/// <summary>One file of the package as listed in the output table.</summary>
/// <param name="Icon">The rendered image's spec; null for text files (Contents.json, XML).</param>
public sealed record PackageFile(IconPlatform Platform, string Path, string Use, IconSpec? Icon);

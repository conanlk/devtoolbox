using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DevToolBox.Core.Tools.AppIcons;

/// <summary>Builds the text files and the ZIP that go with the rendered PNGs.</summary>
public static class AppIconPackage
{
    /// <summary>Xcode's AppIcon.appiconset/Contents.json for <see cref="AppIconCatalog.IosSlots"/>.</summary>
    public static string IosContentsJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteStartArray("images");
            foreach (var slot in AppIconCatalog.IosSlots)
            {
                json.WriteStartObject();
                json.WriteString("filename", slot.Filename);
                json.WriteString("idiom", slot.Idiom);
                json.WriteString("scale", slot.Scale);
                json.WriteString("size", $"{slot.PointSize}x{slot.PointSize}");
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartObject("info");
            json.WriteString("author", "xcode");
            json.WriteNumber("version", 1);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>mipmap-anydpi-v26/ic_launcher.xml (and _round): background colour + foreground image.</summary>
    public static string AndroidAdaptiveIconXml() =>
        """
        <?xml version="1.0" encoding="utf-8"?>
        <adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
            <background android:drawable="@color/ic_launcher_background" />
            <foreground android:drawable="@mipmap/ic_launcher_foreground" />
        </adaptive-icon>

        """;

    /// <summary>values/ic_launcher_background.xml with the chosen background colour (#RRGGBB).</summary>
    public static string AndroidBackgroundColorXml(string hexColor) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <resources>
            <color name="ic_launcher_background">{hexColor}</color>
        </resources>

        """;

    public static string Readme(AppIconOptions? options = null)
    {
        options ??= AppIconOptions.All;
        var readme = new StringBuilder(
            """
            DevToolBox — app icons
            ======================

            """);
        if (options.Ios)
        {
            readme.Append(
                """

                ios/AppIcon.appiconset/
                  Copy the whole folder into your asset catalog, replacing the existing one:
                    Xcode:         <App>/Assets.xcassets/AppIcon.appiconset
                    React Native:  ios/<App>/Images.xcassets/AppIcon.appiconset

                """);
        }

        if (options.Android)
        {
            readme.Append(
                """

                android/res/
                  Merge into your module's res folder (keeps existing files, replaces launcher icons):
                    Android Studio / React Native:  android/app/src/main/res/
                  AndroidManifest.xml should reference android:icon="@mipmap/ic_launcher"
                """);
            readme.Append(options.RoundIcon
                ? " and\n  android:roundIcon=\"@mipmap/ic_launcher_round\" (the defaults for new projects).\n"
                : ".\n");
            if (options.AdaptiveIcon)
            {
                readme.Append("  mipmap-anydpi-v26/ holds the adaptive icon (Android 8+); its background colour is in values/ic_launcher_background.xml.\n");
            }
        }

        if (options.Android && options.PlayStoreIcon)
        {
            readme.Append(
                """

                android/play-store-512.png
                  Upload as the app icon in the Google Play Console.

                """);
        }

        return readme.ToString();
    }

    /// <summary>Every file of the package except README.txt, in display order (iOS first, then Android by density).</summary>
    public static IReadOnlyList<PackageFile> Files(AppIconOptions? options = null)
    {
        options ??= AppIconOptions.All;
        var files = new List<PackageFile>();
        foreach (var spec in AppIconCatalog.Ios.Where(options.Includes))
        {
            files.Add(new PackageFile(IconPlatform.Ios, spec.Path, spec.Label, spec));
        }

        files.AddRange(TextEntries(options).Where(e => e.File.Platform == IconPlatform.Ios).Select(e => e.File));
        foreach (var spec in AppIconCatalog.Android.Where(s => options.Includes(s) && s.Path != AppIconCatalog.PlayStorePath))
        {
            files.Add(new PackageFile(IconPlatform.Android, spec.Path, spec.Label, spec));
        }

        files.AddRange(TextEntries(options).Where(e => e.File.Platform == IconPlatform.Android).Select(e => e.File));
        files.AddRange(AppIconCatalog.Android
            .Where(s => options.Includes(s) && s.Path == AppIconCatalog.PlayStorePath)
            .Select(s => new PackageFile(IconPlatform.Android, s.Path, s.Label, s)));
        return files;
    }

    /// <summary>All non-image files of the package, by ZIP path.</summary>
    public static IReadOnlyDictionary<string, string> TextFiles(string backgroundHex, AppIconOptions? options = null)
    {
        options ??= AppIconOptions.All;
        var files = TextEntries(options).ToDictionary(e => e.File.Path, e => e.Content(backgroundHex), StringComparer.Ordinal);
        files["README.txt"] = Readme(options);
        return files;
    }

    /// <summary>Writes the ZIP: every rendered PNG by its spec path, plus <see cref="TextFiles"/>.</summary>
    public static void WriteZip(Stream destination, IReadOnlyDictionary<string, byte[]> images, string backgroundHex, AppIconOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(images);
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (path, bytes) in images.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            // PNGs are already compressed; storing them avoids wasted CPU.
            using var entry = zip.CreateEntry(path, CompressionLevel.NoCompression).Open();
            entry.Write(bytes);
        }

        foreach (var (path, text) in TextFiles(backgroundHex, options))
        {
            using var entry = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
            entry.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    private static IEnumerable<(PackageFile File, Func<string, string> Content)> TextEntries(AppIconOptions options)
    {
        if (options.Ios)
        {
            yield return (new PackageFile(IconPlatform.Ios, $"{AppIconCatalog.IosFolder}/Contents.json", "Asset catalog manifest", null),
                _ => IosContentsJson());
        }

        if (options.Android && options.AdaptiveIcon)
        {
            var anydpi = $"{AppIconCatalog.AndroidResFolder}/mipmap-anydpi-v26";
            yield return (new PackageFile(IconPlatform.Android, $"{anydpi}/ic_launcher.xml", "Adaptive icon definition", null),
                _ => AndroidAdaptiveIconXml());
            if (options.RoundIcon)
            {
                yield return (new PackageFile(IconPlatform.Android, $"{anydpi}/ic_launcher_round.xml", "Adaptive round icon definition", null),
                    _ => AndroidAdaptiveIconXml());
            }

            yield return (new PackageFile(IconPlatform.Android, $"{AppIconCatalog.AndroidResFolder}/values/ic_launcher_background.xml",
                "Adaptive background colour", null), AndroidBackgroundColorXml);
        }
    }
}

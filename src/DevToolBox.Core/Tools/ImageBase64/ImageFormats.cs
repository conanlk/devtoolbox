using System.Text;

namespace DevToolBox.Core.Tools.ImageBase64;

/// <summary>Identifies image formats by their file signature ("magic bytes").</summary>
public static class ImageFormats
{
    public static readonly ImageFormatInfo Png = new("image/png", "png");
    public static readonly ImageFormatInfo Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageFormatInfo Gif = new("image/gif", "gif");
    public static readonly ImageFormatInfo WebP = new("image/webp", "webp");
    public static readonly ImageFormatInfo Bmp = new("image/bmp", "bmp");
    public static readonly ImageFormatInfo Ico = new("image/x-icon", "ico");
    public static readonly ImageFormatInfo Svg = new("image/svg+xml", "svg");
    public static readonly ImageFormatInfo Avif = new("image/avif", "avif");
    public static readonly ImageFormatInfo Tiff = new("image/tiff", "tiff");

    public static ImageFormatInfo? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return Gif;
        }

        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        if (data.StartsWith("BM"u8))
        {
            return Bmp;
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0x01, 0x00]))
        {
            return Ico;
        }

        if (data.Length >= 12 && data[4..8].SequenceEqual("ftyp"u8) && (data[8..12].SequenceEqual("avif"u8) || data[8..12].SequenceEqual("avis"u8)))
        {
            return Avif;
        }

        if (data.StartsWith("II*\0"u8) || data.StartsWith("MM\0*"u8))
        {
            return Tiff;
        }

        return LooksLikeSvg(data) ? Svg : null;
    }

    public static ImageFormatInfo? FromFileName(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant() switch
        {
            "png" => Png,
            "jpg" or "jpeg" => Jpeg,
            "gif" => Gif,
            "webp" => WebP,
            "bmp" => Bmp,
            "ico" => Ico,
            "svg" => Svg,
            "avif" => Avif,
            "tif" or "tiff" => Tiff,
            _ => null,
        };

    private static bool LooksLikeSvg(ReadOnlySpan<byte> data)
    {
        var head = Encoding.UTF8.GetString(data[..Math.Min(data.Length, 1024)]).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
               || ((head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || head.StartsWith("<!--", StringComparison.Ordinal))
                   && head.Contains("<svg", StringComparison.OrdinalIgnoreCase));
    }
}

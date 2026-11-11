using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Codecs;
using DevToolBox.Core.Tools.Url;

namespace DevToolBox.Core.Tools.ImageBase64;

public sealed record ImageFormatInfo(string MimeType, string Extension);

public sealed record ImageEncoding(string MimeType, long SizeBytes, string Base64, string DataUri)
{
    public string HtmlImgTag => $"<img src=\"{DataUri}\" alt=\"\" />";

    public string CssUrl => $"background-image: url(\"{DataUri}\");";
}

public sealed record DecodedImage(byte[] Bytes, ImageFormatInfo Format);

public static class ImageBase64Converter
{
    /// <summary>Larger images make data URIs impractical and the UI sluggish.</summary>
    public const long MaxImageBytes = 25 * 1024 * 1024;

    /// <summary>Above this size the tool warns that a data URI is a poor choice.</summary>
    public const long WarnImageBytes = 1024 * 1024;

    public static Result<ImageEncoding> Encode(byte[] bytes, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            return new Error("image.empty", CoreStrings.Image_FileEmpty);
        }

        if (bytes.Length > MaxImageBytes)
        {
            return new Error("image.too-large", CoreLocalization.Format(CoreStrings.Image_TooLarge, MaxImageBytes / (1024 * 1024)));
        }

        var format = ImageFormats.Detect(bytes) ?? ImageFormats.FromFileName(fileName);
        if (format is null)
        {
            return new Error("image.unknown-format", CoreStrings.Image_UnknownFormat);
        }

        var base64 = Base64Codec.Encode(bytes);
        return new ImageEncoding(format.MimeType, bytes.Length, base64, $"data:{format.MimeType};base64,{base64}");
    }

    /// <summary>Accepts raw Base64 or a full data URI (Base64 or percent-encoded).</summary>
    public static Result<DecodedImage> Decode(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new Error("image.empty-input", CoreStrings.Image_EmptyInput);
        }

        byte[] bytes;
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = text.IndexOf(',', StringComparison.Ordinal);
            if (comma < 0)
            {
                return new Error("image.data-uri", CoreStrings.Image_MalformedDataUri);
            }

            var header = text[5..comma];
            var payload = text[(comma + 1)..];
            if (header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                if (!Base64Codec.TryDecode(payload, out bytes))
                {
                    return InvalidBase64();
                }
            }
            else
            {
                var decoded = UrlCodec.Decode(payload, UrlEncodeMode.Component);
                if (!decoded.IsOk)
                {
                    return decoded.Error;
                }

                bytes = Encoding.UTF8.GetBytes(decoded.Value);
            }
        }
        else if (!Base64Codec.TryDecode(text, out bytes))
        {
            return InvalidBase64();
        }

        if (bytes.Length == 0)
        {
            return new Error("image.empty", CoreStrings.Image_DataEmpty);
        }

        return ImageFormats.Detect(bytes) is { } format
            ? new DecodedImage(bytes, format)
            : new Error("image.unknown-format", CoreStrings.Image_DecodedUnknownFormat);
    }

    private static Error InvalidBase64() => new("image.base64", CoreStrings.Image_InvalidBase64);
}

using System.Text;
using DevToolBox.Core.Tools.ImageBase64;

namespace DevToolBox.Core.Tests.Tools.ImageBase64;

public class ImageBase64ConverterTests
{
    // 1×1 transparent PNG.
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
    private static readonly byte[] Png = Convert.FromBase64String(PngBase64);

    [Fact]
    public void Encode_ProducesDataUriAndSnippets()
    {
        var result = ImageBase64Converter.Encode(Png, "dot.png").Value;

        Assert.Equal("image/png", result.MimeType);
        Assert.Equal(PngBase64, result.Base64);
        Assert.Equal($"data:image/png;base64,{PngBase64}", result.DataUri);
        Assert.Equal($"<img src=\"data:image/png;base64,{PngBase64}\" alt=\"\" />", result.HtmlImgTag);
        Assert.StartsWith("background-image: url(\"data:image/png;base64,", result.CssUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x42, 0x4D, 0, 0 }, "image/bmp")]
    [InlineData(new byte[] { 0, 0, 1, 0, 1, 0 }, "image/x-icon")]
    public void Detect_RecognizesSignatures(byte[] header, string mime) =>
        Assert.Equal(mime, ImageFormats.Detect(header)?.MimeType);

    [Fact]
    public void Detect_RecognizesWebPAndSvg()
    {
        Assert.Equal("image/webp", ImageFormats.Detect("RIFF\0\0\0\0WEBPVP8 "u8)?.MimeType);
        Assert.Equal("image/svg+xml", ImageFormats.Detect(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\n<svg xmlns=\"…\"/>"))?.MimeType);
        Assert.Null(ImageFormats.Detect("hello"u8));
    }

    [Fact]
    public void Encode_UnknownFormat_IsAnError() =>
        Assert.Equal("image.unknown-format", ImageBase64Converter.Encode("not an image"u8.ToArray(), "notes.txt").Error?.Code);

    [Fact]
    public void Encode_EmptyFile_IsAnError() => Assert.Equal("image.empty", ImageBase64Converter.Encode([]).Error?.Code);

    [Theory]
    [InlineData("data:image/png;base64," + PngBase64)]
    [InlineData(PngBase64)]
    [InlineData("  data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4\n2mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=  ")]
    public void Decode_AcceptsDataUriOrRawBase64(string input)
    {
        var result = ImageBase64Converter.Decode(input).Value;

        Assert.Equal(Png, result.Bytes);
        Assert.Equal("png", result.Format.Extension);
    }

    [Fact]
    public void Decode_PercentEncodedSvgDataUri()
    {
        var result = ImageBase64Converter.Decode("data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%2F%3E").Value;

        Assert.Equal("image/svg+xml", result.Format.MimeType);
    }

    [Theory]
    [InlineData("", "image.empty-input")]
    [InlineData("data:image/png;base64", "image.data-uri")]
    [InlineData("@@@", "image.base64")]
    [InlineData("SGVsbG8=", "image.unknown-format")]
    public void Decode_Invalid_GivesClearError(string input, string code) =>
        Assert.Equal(code, ImageBase64Converter.Decode(input).Error?.Code);
}

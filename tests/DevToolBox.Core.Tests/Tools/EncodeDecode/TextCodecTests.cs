using DevToolBox.Core.Tools.EncodeDecode;

namespace DevToolBox.Core.Tests.Tools.EncodeDecode;

public class TextCodecTests
{
    [Theory]
    [InlineData(TextEncoding.Base64, "Xin chào 👋", "WGluIGNow6BvIPCfkYs=")]
    [InlineData(TextEncoding.Base64Url, "??>", "Pz8-")]
    [InlineData(TextEncoding.Hex, "Hi", "4869")]
    [InlineData(TextEncoding.Binary, "Hi", "01001000 01101001")]
    [InlineData(TextEncoding.Base64, "", "")]
    public void Encode_ThenDecode_RoundTrips(TextEncoding encoding, string text, string encoded)
    {
        Assert.Equal(encoded, TextCodec.Encode(text, encoding));
        Assert.Equal(text, TextCodec.Decode(encoded, encoding).Value);
    }

    [Theory]
    [InlineData("0x48 0x69", "Hi")]
    [InlineData("48:69", "Hi")]
    [InlineData("4 8 6 9", "Hi")]
    public void DecodeHex_AcceptsCommonSeparators(string input, string expected) =>
        Assert.Equal(expected, TextCodec.Decode(input, TextEncoding.Hex).Value);

    [Theory]
    [InlineData(TextEncoding.Hex, "486", "codec.invalid-hex")]
    [InlineData(TextEncoding.Hex, "zz", "codec.invalid-hex")]
    [InlineData(TextEncoding.Binary, "0101", "codec.invalid-binary")]
    [InlineData(TextEncoding.Binary, "01001002", "codec.invalid-binary")]
    [InlineData(TextEncoding.Base64, "@@@@", "codec.invalid-base64")]
    [InlineData(TextEncoding.Hex, "ff", "codec.not-utf8")]
    public void Decode_InvalidInput_ReturnsError(TextEncoding encoding, string input, string code) =>
        Assert.Equal(code, TextCodec.Decode(input, encoding).Error?.Code);

    [Theory]
    [InlineData("SGVsbG8gV29ybGQ=", TextEncoding.Base64, CodecDirection.Decode)]
    [InlineData("Hello World", TextEncoding.Base64, CodecDirection.Encode)]
    [InlineData("test", TextEncoding.Base64, CodecDirection.Encode)]
    [InlineData("48656c6c6f", TextEncoding.Hex, CodecDirection.Decode)]
    [InlineData("cafe", TextEncoding.Hex, CodecDirection.Encode)]
    [InlineData("01001000 01101001", TextEncoding.Binary, CodecDirection.Decode)]
    [InlineData("", TextEncoding.Base64, CodecDirection.Encode)]
    public void DetectDirection_GuessesSensibly(string input, TextEncoding encoding, CodecDirection expected) =>
        Assert.Equal(expected, TextCodec.DetectDirection(input, encoding));

    [Fact]
    public void LargeInput_RoundTrips()
    {
        var text = new string('é', 2_000_000);
        var encoded = TextCodec.Encode(text, TextEncoding.Base64);
        Assert.Equal(text, TextCodec.Decode(encoded, TextEncoding.Base64).Value);
    }
}

using DevToolBox.Core.Shared.Codecs;

namespace DevToolBox.Core.Tests.Shared.Codecs;

public class Base64CodecTests
{
    [Theory]
    [InlineData("SGVsbG8=", "Hello")]
    [InlineData("SGVsbG8", "Hello")]
    [InlineData("SGVs\nbG8=", "Hello")]
    [InlineData("-_8", "ûÿ")]
    public void TryDecode_AcceptsPaddingVariantsWhitespaceAndUrlAlphabet(string input, string expectedLatin1)
    {
        Assert.True(Base64Codec.TryDecode(input, out var bytes));
        Assert.Equal(expectedLatin1, System.Text.Encoding.Latin1.GetString(bytes));
    }

    [Theory]
    [InlineData("abc!")]
    [InlineData("a")]
    [InlineData("ab=c")]
    [InlineData("ab===")]
    [InlineData(null)]
    public void TryDecode_RejectsInvalidInput(string? input) => Assert.False(Base64Codec.TryDecode(input, out _));

    [Fact]
    public void EncodeUrl_HasNoPaddingOrUnsafeCharacters() =>
        Assert.Equal("-_8", Base64Codec.EncodeUrl([0xFB, 0xFF]));
}

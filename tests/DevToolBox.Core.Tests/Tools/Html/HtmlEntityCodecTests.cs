using System.Net;
using DevToolBox.Core.Tools.Html;

namespace DevToolBox.Core.Tests.Tools.Html;

public class HtmlEntityCodecTests
{
    [Fact]
    public void Encode_UnsafeOnly_EscapesMarkupCharactersOnly() =>
        Assert.Equal("&lt;a href=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s café&lt;/a&gt;",
            HtmlEntityCodec.Encode("<a href=\"x\">Tom & Jerry's café</a>", HtmlEncodeScope.UnsafeOnly, preferNamedEntities: true));

    [Fact]
    public void Encode_AllNonAscii_UsesNamedEntitiesWhenPreferred() =>
        Assert.Equal("caf&eacute; &copy; &euro;", HtmlEntityCodec.Encode("café © €", HtmlEncodeScope.AllNonAscii, preferNamedEntities: true));

    [Fact]
    public void Encode_AllNonAscii_NumericForEmojiAndWhenNamedNotPreferred()
    {
        Assert.Equal("caf&#xE9; &#x1F600;", HtmlEntityCodec.Encode("café 😀", HtmlEncodeScope.AllNonAscii, preferNamedEntities: false));
        Assert.Equal("&#x1F600;", HtmlEntityCodec.Encode("😀", HtmlEncodeScope.AllNonAscii, preferNamedEntities: true));
    }

    [Theory]
    [InlineData("&lt;b&gt; &amp;amp; &eacute; &#233; &#xE9; &#x1F600;", "<b> &amp; é é é 😀")]
    [InlineData("no entities", "no entities")]
    [InlineData("&unknown; &", "&unknown; &")]
    [InlineData("", "")]
    public void Decode_HandlesNamedAndNumericReferences(string input, string expected) =>
        Assert.Equal(expected, HtmlEntityCodec.Decode(input));

    [Fact]
    public void EveryNamedEntityUsedForEncoding_IsUnderstoodByDecoders()
    {
        foreach (var (codePoint, name) in HtmlNamedEntitiesAccessor.All)
        {
            Assert.Equal(char.ConvertFromUtf32(codePoint), WebUtility.HtmlDecode($"&{name};"));
        }
    }

    [Theory]
    [InlineData("<script>alert('x')</script> & ünïcödé ✓")]
    public void EncodeThenDecode_RoundTrips(string text)
    {
        foreach (var scope in Enum.GetValues<HtmlEncodeScope>())
        {
            Assert.Equal(text, HtmlEntityCodec.Decode(HtmlEntityCodec.Encode(text, scope, preferNamedEntities: true)));
        }
    }
}

using DevToolBox.Core.Tools.Url;

namespace DevToolBox.Core.Tests.Tools.Url;

public class UrlCodecTests
{
    [Theory]
    [InlineData("a b&c=d/é", UrlEncodeMode.Component, "a%20b%26c%3Dd%2F%C3%A9")]
    [InlineData("a b&c=d/é", UrlEncodeMode.FullUri, "a%20b&c=d/%C3%A9")]
    [InlineData("-_.!~*'()", UrlEncodeMode.Component, "-_.!~*'()")]
    [InlineData("😀", UrlEncodeMode.Component, "%F0%9F%98%80")]
    [InlineData("", UrlEncodeMode.Component, "")]
    public void Encode_MatchesJavaScriptSemantics(string input, UrlEncodeMode mode, string expected) =>
        Assert.Equal(expected, UrlCodec.Encode(input, mode).Value);

    [Fact]
    public void Encode_SpaceAsPlus_ForForms() =>
        Assert.Equal("a+b", UrlCodec.Encode("a b", UrlEncodeMode.Component, spaceAsPlus: true).Value);

    [Fact]
    public void Encode_LoneSurrogate_IsAnError() =>
        Assert.Equal("url.lone-surrogate", UrlCodec.Encode("a\uD800b", UrlEncodeMode.Component).Error?.Code);

    [Theory]
    [InlineData("a%20b%26c%2F%C3%A9", UrlEncodeMode.Component, false, "a b&c/é")]
    [InlineData("a%20b%26c%2F", UrlEncodeMode.FullUri, false, "a b%26c%2F")]
    [InlineData("a+b", UrlEncodeMode.Component, true, "a b")]
    [InlineData("a+b", UrlEncodeMode.Component, false, "a+b")]
    [InlineData("%F0%9F%98%80", UrlEncodeMode.Component, false, "😀")]
    public void Decode_HandlesModesAndPlus(string input, UrlEncodeMode mode, bool plusAsSpace, string expected) =>
        Assert.Equal(expected, UrlCodec.Decode(input, mode, plusAsSpace).Value);

    [Theory]
    [InlineData("100%", "url.malformed-escape", 4)]
    [InlineData("%zz", "url.malformed-escape", 1)]
    [InlineData("ok%C3", "url.invalid-utf8", 3)]
    public void Decode_ReportsPositionOfBadEscape(string input, string code, int position)
    {
        var error = UrlCodec.Decode(input, UrlEncodeMode.Component).Error;

        Assert.Equal(code, error?.Code);
        Assert.Equal(position - 1, error?.Location?.Offset);
    }
}

public class UrlParserTests
{
    [Fact]
    public void Parse_SplitsAllParts()
    {
        var url = UrlParser.Parse("https://user:pw@Example.com:8443/a/b?q=hello+world&x=%C3%A9&flag#top").Value;

        Assert.Equal("https", url.Scheme);
        Assert.Equal("user:pw", url.UserInfo);
        Assert.Equal("example.com", url.Host);
        Assert.Equal(8443, url.Port);
        Assert.Equal("/a/b", url.Path);
        Assert.Equal([new("q", "hello world"), new("x", "é"), new("flag", "")], url.Query);
        Assert.Equal("top", url.Fragment);
    }

    [Fact]
    public void Parse_DefaultPortIsNull() => Assert.Null(UrlParser.Parse("https://example.com").Value.Port);

    [Theory]
    [InlineData("")]
    [InlineData("example.com/path")]
    [InlineData("not a url")]
    public void Parse_RelativeOrInvalid_IsAnError(string input) =>
        Assert.Equal("url.not-absolute", UrlParser.Parse(input).Error?.Code);

    [Fact]
    public void Build_AfterEditingQuery_EncodesValues()
    {
        var url = UrlParser.Parse("https://example.com/search?q=a").Value;
        var edited = url with { Query = [new("q", "a b&c"), new("lang", "vi")] };

        Assert.Equal("https://example.com/search?q=a%20b%26c&lang=vi", UrlParser.Build(edited));
    }
}

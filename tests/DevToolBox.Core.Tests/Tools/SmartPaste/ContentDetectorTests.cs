using DevToolBox.Core.Tools.SmartPaste;

namespace DevToolBox.Core.Tests.Tools.SmartPaste;

public class ContentDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    private const string Jwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private static ContentDetection Detect(string text) => ContentDetector.Detect(text, Now);

    [Theory]
    [InlineData(Jwt, "JWT", "jwt-decoder", null)]
    [InlineData("Bearer " + Jwt, "JWT", "jwt-decoder", null)]
    [InlineData("{\"a\": [1, 2]}", "JSON", "json-formatter", null)]
    [InlineData("[1,2,3]", "JSON", "json-formatter", null)]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", "UUID", "uuid-generator", "inspect")]
    [InlineData("1792049400", "Unix timestamp", "timestamp", null)]
    [InlineData("1792049400123", "Unix timestamp", "timestamp", null)]
    [InlineData("data:image/png;base64,iVBORw0KGgo=", "Image data URI", "image-base64", "decode")]
    [InlineData("https://example.com/a?b=1", "URL", "url-encode", "parse")]
    [InlineData("name%3DNguy%E1%BB%85n", "URL-encoded text", "url-encode", "decode")]
    [InlineData("Tom &amp; Jerry &lt;3", "HTML entities", "html-encode", "decode")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "Hash", "hash-generator", "compare")]
    [InlineData("SGVsbG8sIERldlRvb2xib3gh", "Base64", "encode-decode", "decode")]
    [InlineData("just some words", "Text", "hash-generator", null)]
    [InlineData("<b>bold</b>", "Text", "html-encode", "encode")]
    public void Detect_RecognizesContent_AndRanksBestToolFirst(string text, string kind, string firstTool, string? intent)
    {
        var detection = Detect(text);

        Assert.Equal(kind, detection.Kind);
        Assert.Equal(firstTool, detection.Suggestions[0].ToolId);
        Assert.Equal(intent, detection.Suggestions[0].Intent);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("abcdefgh")]
    public void Detect_PlainWordsThatHappenToBeValidBase64_AreText(string word) =>
        Assert.Equal("Text", Detect(word).Kind);

    [Fact]
    public void Detect_MalformedJwtShape_IsNotAJwt() =>
        Assert.NotEqual("JWT", Detect("abcd.efgh.ijkl").Kind);

    [Fact]
    public void Detect_Url_AlsoOffersQrCode() =>
        Assert.Contains(Detect("https://example.com").Suggestions, s => s.ToolId == "barcode-generator");

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData(null)]
    public void Detect_Empty_HasNoSuggestions(string? text) => Assert.Empty(ContentDetector.Detect(text, Now).Suggestions);

    [Fact]
    public void Detect_NeverReturnsMoreThanThreeDistinctSuggestions()
    {
        foreach (var text in new[] { Jwt, "hello world", "https://example.com", "{}" })
        {
            var suggestions = Detect(text).Suggestions;
            Assert.InRange(suggestions.Count, 1, 3);
            Assert.Equal(suggestions.Count, suggestions.DistinctBy(s => (s.ToolId, s.Intent)).Count());
        }
    }

    [Fact]
    public void Detect_HugeInput_SkipsExpensiveChecksButStillSuggests()
    {
        var huge = "{" + new string('x', ContentDetector.MaxDeepInspectionChars) + "}";

        var detection = Detect(huge);

        Assert.Equal("Text", detection.Kind);
        Assert.NotEmpty(detection.Suggestions);
    }
}

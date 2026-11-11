using DevToolBox.Core.Shared.Json;
using DevToolBox.Core.Tools.Json;

namespace DevToolBox.Core.Tests.Shared.Json;

public class JsonFormatterTests
{
    private static string Format(string json, JsonIndent indent = JsonIndent.TwoSpaces, bool sort = false) =>
        JsonFormatter.Format(json, new JsonFormatOptions(indent, sort)).Value.Text;

    [Fact]
    public void Beautify_TwoSpaces()
    {
        Assert.Equal("{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null\n  ],\n  \"c\": {}\n}",
            Format("{\"a\":1,\"b\":[true,null],\"c\":{}}"));
    }

    [Theory]
    [InlineData(JsonIndent.FourSpaces, "[\n    1\n]")]
    [InlineData(JsonIndent.Tab, "[\n\t1\n]")]
    [InlineData(JsonIndent.Minified, "[1]")]
    public void Indent_Options(JsonIndent indent, string expected) => Assert.Equal(expected, Format(" [ 1 ] ", indent));

    [Fact]
    public void Minify_RemovesAllWhitespaceOutsideStrings() =>
        Assert.Equal("{\"a b\":[1,2],\"c\":\"x y\"}", Format("{ \"a b\" : [ 1 , 2 ] ,\n \"c\" : \"x y\" }", JsonIndent.Minified));

    [Fact]
    public void PreservesNumbersAndStringEscapesExactly()
    {
        const string json = "{\"id\":12345678901234567890,\"f\":1.0e+10,\"s\":\"caf\\u00e9 \\\"q\\\"\",\"u\":\"😀\"}";

        Assert.Equal(json, Format(json, JsonIndent.Minified));
    }

    [Fact]
    public void SortKeys_IsRecursiveAndOrdinal() =>
        Assert.Equal("{\"B\":1,\"a\":{\"x\":1,\"y\":2},\"b\":[{\"c\":2,\"d\":1}]}",
            Format("{\"b\":[{\"d\":1,\"c\":2}],\"a\":{\"y\":2,\"x\":1},\"B\":1}", JsonIndent.Minified, sort: true));

    [Theory]
    [InlineData("{\"a\":1,}", 1, 8)]
    [InlineData("{\n  \"a\": tru\n}", 2, 11)]
    [InlineData("[1,2", 1, 5)]
    [InlineData("{} {}", 1, 4)]
    [InlineData("{\"é\": x}", 1, 7)]
    public void InvalidJson_ReportsLineAndColumn(string json, int line, int column)
    {
        var error = JsonFormatter.Format(json, new JsonFormatOptions()).Error!;

        Assert.Equal("json.invalid", error.Code);
        Assert.Equal(line, error.Location?.Line);
        Assert.Equal(column, error.Location?.Column);
        Assert.StartsWith($"Line {line}, column {column}: ", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_IsAnError(string json) =>
        Assert.Equal("json.empty", JsonFormatter.Format(json, new JsonFormatOptions()).Error?.Code);

    [Theory]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void ScalarRoots_AreValid(string json) => Assert.Equal(json, Format(json));

    [Fact]
    public void LargeInput_FormatsAndCountsNodes()
    {
        var items = string.Join(",", Enumerable.Range(0, 150_000).Select(i => $"{{\"id\":{i},\"name\":\"item {i}\",\"tags\":[\"a\",\"b\"]}}"));
        var json = $"[{items}]";
        Assert.True(json.Length > 5_000_000);

        var result = JsonFormatter.Format(json, new JsonFormatOptions()).Value;

        Assert.Equal(1 + (150_000 * 6), result.NodeCount); // object + id + name + tags + 2 tag strings
        Assert.Equal(json, JsonFormatter.Format(result.Text, new JsonFormatOptions(JsonIndent.Minified)).Value.Text);
    }

    [Fact]
    public void Cancellation_IsHonored()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var json = "[" + string.Join(",", Enumerable.Repeat("1", 10_000)) + "]";

        Assert.Throws<OperationCanceledException>(() => JsonFormatter.Format(json, new JsonFormatOptions(), cts.Token));
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("[]", 1)]
    [InlineData("{\"a\":[{\"b\":1}]}", 3)]
    public void Format_ReportsMaxDepth(string json, int depth) =>
        Assert.Equal(depth, JsonFormatter.Format(json, new JsonFormatOptions()).Value.MaxDepth);

    [Fact]
    public void Parse_BuildsTreeWithDecodedKeys()
    {
        var root = JsonTreeParser.Parse("{\"caf\\u00e9\":[1,\"x\"]}").Value.Root;

        Assert.Equal(JsonNodeKind.Object, root.Kind);
        Assert.Equal("café", root.Properties![0].Key);
        Assert.Equal("caf\\u00e9", root.Properties[0].RawKey);
        Assert.Equal(2, root.Properties[0].Value.ChildCount);
    }
}

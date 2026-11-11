using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void Find_ReturnsToolById_OrNull()
    {
        var registry = new ToolRegistry(FakeTools.Catalog);

        Assert.Equal("JWT Decoder", registry.Find("jwt-decoder")?.Name);
        Assert.Null(registry.Find("missing"));
    }

    [Fact]
    public void DuplicateIds_Throw()
    {
        var tool = FakeTools.Create("dup", "Dup", ToolCategory.FormatAndTest);

        Assert.Throws<ArgumentException>(() => new ToolRegistry([tool, tool]));
    }

    [Fact]
    public void ShippedCatalog_HasUniqueKebabCaseIds_AndKnownIcons()
    {
        var registry = new ToolRegistry(ToolCatalog.All);

        Assert.NotEmpty(registry.All);
        Assert.All(registry.All, tool =>
        {
            Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", tool.Id);
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.InRange(tool.Badge.Length, 1, 4);
            Assert.False(string.IsNullOrWhiteSpace(tool.ShortName));
            Assert.EndsWith(".", tool.Description, StringComparison.Ordinal);
        });
    }
}

public class ToolSearchTests
{
    private static readonly ToolDefinition Jwt = FakeTools.Create("jwt", "JWT Decoder", ToolCategory.EncodeDecode, "token");

    [Theory]
    [InlineData(null, true)]
    [InlineData("   ", true)]
    [InlineData("jwt", true)]
    [InlineData("decoder", true)]
    [InlineData("TOKEN", true)]
    [InlineData("jwt token", true)]
    [InlineData("jwt hash", false)]
    [InlineData("hash", false)]
    public void Matches_AllTermsInNameOrKeywords(string? query, bool expected) =>
        Assert.Equal(expected, ToolSearch.Matches(Jwt, query));
}

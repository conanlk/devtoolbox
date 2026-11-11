using System.Text.RegularExpressions;
using DevToolBox.Core.Tools.RegularExpressions;

namespace DevToolBox.Core.Tests.Tools.RegularExpressions;

public class RegexTesterTests
{
    [Fact]
    public void Run_ReturnsMatchesWithNumberedAndNamedGroups()
    {
        var result = RegexTester.Run(@"(?<user>\w+)@(\w+)\.com", "a@x.com, bob@yy.com", RegexOptions.None).Value;

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(["1", "user"], result.GroupNames);
        var second = result.Matches[1];
        Assert.Equal((9, "bob@yy.com"), (second.Index, second.Value));
        Assert.Equal("yy", second.Groups.Single(g => g.Number == 1).Value);
        Assert.Equal("bob", second.Groups.Single(g => g.Name == "user").Value);
    }

    [Fact]
    public void Run_OptionsAffectMatching()
    {
        Assert.Empty(RegexTester.Run("abc", "ABC", RegexOptions.None).Value.Matches);
        Assert.Single(RegexTester.Run("abc", "ABC", RegexOptions.IgnoreCase).Value.Matches);
        Assert.Equal(2, RegexTester.Run("^x", "x\nx", RegexOptions.Multiline).Value.Matches.Count);
    }

    [Fact]
    public void Run_ReplacePreview()
    {
        var result = RegexTester.Run(@"(\w+)@(\w+)", "a@b c@d", RegexOptions.None, replacement: "$2 at $1").Value;

        Assert.Equal("b at a d at c", result.ReplaceResult);
    }

    [Fact]
    public void Run_EmptyMatches_DoNotLoopForever()
    {
        var result = RegexTester.Run("x*", "abc", RegexOptions.None).Value;

        Assert.Equal(4, result.Matches.Count);
    }

    [Fact]
    public void Run_TruncatesAtMaxMatches()
    {
        var result = RegexTester.Run(".", new string('a', 50), RegexOptions.None, maxMatches: 10).Value;

        Assert.Equal(10, result.Matches.Count);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public void Run_InvalidPattern_ReportsOffset()
    {
        var error = RegexTester.Run("(abc", "x", RegexOptions.None).Error!;

        Assert.Equal("regex.invalid", error.Code);
        Assert.Equal(4, error.Location?.Offset);
    }

    [Fact]
    public void Run_CatastrophicBacktracking_TimesOut()
    {
        var error = RegexTester.Run("(a+)+$", new string('a', 40) + "!", RegexOptions.None, timeout: TimeSpan.FromMilliseconds(200)).Error;

        Assert.Equal("regex.timeout", error?.Code);
    }

    [Fact]
    public void Run_NonBacktracking_HandlesTheSamePatternQuickly()
    {
        var result = RegexTester.Run("(a+)+$", new string('a', 40) + "!", RegexOptions.NonBacktracking, timeout: TimeSpan.FromSeconds(2));

        Assert.True(result.IsOk);
        Assert.Empty(result.Value.Matches);
    }

    [Fact]
    public void Run_NonBacktrackingWithBackreference_IsUnsupported() =>
        Assert.Equal("regex.unsupported", RegexTester.Run(@"(a)\1", "aa", RegexOptions.NonBacktracking).Error?.Code);

    [Fact]
    public void Run_UnicodeInput_UsesUtf16Indexes()
    {
        var match = RegexTester.Run("é", "😀é", RegexOptions.None).Value.Matches.Single();

        Assert.Equal(2, match.Index);
    }
}

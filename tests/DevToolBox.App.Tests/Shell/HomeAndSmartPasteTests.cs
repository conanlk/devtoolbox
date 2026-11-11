using Avalonia.Headless.XUnit;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;
using DevToolBox.App.Tools.Hash;
using DevToolBox.App.Tools.ImageBase64;
using DevToolBox.App.Tools.Json;
using DevToolBox.App.Tools.Jwt;
using DevToolBox.App.Tools.Timestamp;
using DevToolBox.App.Tools.Url;

namespace DevToolBox.App.Tests.Shell;

public class HomeViewModelTests
{
    [Fact]
    public void Filters_ShowOnlyMatchingCategory()
    {
        using var services = new TestServices();
        var home = services.Get<ShellViewModel>().Home;

        Assert.Equal(["All", "Convert", "Encode", "Format & test", "Generate"], home.Filters.Select(f => f.Label));
        Assert.Equal(13, home.VisibleCards.Count);
        Assert.True(home.Filters[0].IsSelected);

        home.Filters.Single(f => f.Label == "Format & test").SelectCommand!.Execute(null);

        Assert.Equal(["json-formatter", "regex-tester"], home.VisibleCards.Select(c => c.ToolId));
        Assert.False(home.Filters[0].IsSelected);
    }

    [Fact]
    public void Favorites_FollowStars_AndFilterSurvivesNavigation()
    {
        using var services = new TestServices();
        var shell = services.Get<ShellViewModel>();
        shell.Home.SelectedFilter = shell.Home.Filters[4];

        shell.ToggleFavorite("uuid-generator");
        shell.OpenTool("timestamp");
        shell.GoHome();

        Assert.Same(shell.Home, shell.CurrentPage);
        Assert.Equal(["uuid-generator"], shell.Home.Favorites.Select(c => c.ToolId));
        Assert.True(shell.Home.HasFavorites);
        Assert.Equal("Generate", shell.Home.SelectedFilter.Label);
    }

    [Fact]
    public void Cards_CarryBadgeCategoryAndDescription()
    {
        using var services = new TestServices();

        var jwt = services.Get<ShellViewModel>().Home.VisibleCards.Single(c => c.ToolId == "jwt-decoder");

        Assert.Equal("JWT", jwt.Badge);
        Assert.Equal("CONVERT", jwt.CategoryLabel);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Description));
    }
}

/// <summary>Smart paste → suggestion → tool opened with the pasted content (real tools, fake platform).</summary>
public class SmartPasteTests
{
    private const string Jwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private static (TestServices Services, ShellViewModel Shell) Paste(string text)
    {
        var services = new TestServices();
        var shell = services.Get<ShellViewModel>();
        shell.Home.SmartPaste.Text = text;
        shell.Home.SmartPaste.Analyze();
        return (services, shell);
    }

    [Fact]
    public void Jwt_PrimarySuggestionOpensDecoderWithToken()
    {
        var (services, shell) = Paste(Jwt);
        using var _ = services;

        Assert.Equal("JWT", shell.Home.SmartPaste.DetectedKind);
        var primary = shell.Home.SmartPaste.Suggestions[0];
        Assert.True(primary.IsPrimary);
        Assert.Equal("Open in JWT decoder →", primary.Label);

        primary.Command.Execute(null);

        var jwt = Assert.IsType<JwtViewModel>(shell.CurrentToolPage!.ToolViewModel);
        Assert.True(jwt.HasToken);
        Assert.Contains("John Doe", jwt.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_OpensFormatterWithInput()
    {
        var (services, shell) = Paste("{\"a\":1}");
        using var _ = services;

        shell.Home.SmartPaste.Suggestions[0].Command.Execute(null);

        var json = Assert.IsType<JsonViewModel>(shell.CurrentToolPage!.ToolViewModel);
        await json.LastRun;
        Assert.Equal("{\n  \"a\": 1\n}", json.Output);
    }

    [Fact]
    public void Url_ParseIntent_SwitchesToParserTab()
    {
        var (services, shell) = Paste("https://example.com/search?q=dev");
        using var _ = services;

        shell.Home.SmartPaste.Suggestions.Single(s => s.Label.StartsWith("Parse URL", StringComparison.Ordinal)).Command.Execute(null);

        var url = Assert.IsType<UrlViewModel>(shell.CurrentToolPage!.ToolViewModel);
        Assert.Equal(1, url.SelectedTab);
        Assert.Equal("q", url.QueryParameters.Single().Key);
    }

    [Fact]
    public void Hash_CompareIntent_FillsExpectedHashAndFileMode()
    {
        var (services, shell) = Paste("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        using var _ = services;

        shell.Home.SmartPaste.Suggestions[0].Command.Execute(null);

        var hash = Assert.IsType<HashViewModel>(shell.CurrentToolPage!.ToolViewModel);
        Assert.Equal(HashSource.File, hash.Source);
        Assert.StartsWith("ba7816bf", hash.ExpectedHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Timestamp_OpensWithEpoch()
    {
        var (services, shell) = Paste("1792049400");
        using var _ = services;

        shell.Home.SmartPaste.Suggestions[0].Command.Execute(null);

        var ts = Assert.IsType<TimestampViewModel>(shell.CurrentToolPage!.ToolViewModel);
        Assert.Equal("1792049400", ts.EpochInput);
        Assert.Equal("Interpreted as seconds", ts.DetectedUnit);
    }

    [AvaloniaFact]
    public void ImageDataUri_OpensDecoderTab()
    {
        var (services, shell) = Paste("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        using var _ = services;

        shell.Home.SmartPaste.Suggestions[0].Command.Execute(null);

        var image = Assert.IsType<ImageBase64ViewModel>(shell.CurrentToolPage!.ToolViewModel);
        Assert.Equal(1, image.SelectedTab);
        Assert.True(image.HasDecodedImage);
    }

    [Fact]
    public void EveryShippedTool_AcceptsInput()
    {
        Assert.All(ToolCatalog.All, tool =>
        {
            using var services = new TestServices();
            var shell = services.Get<ShellViewModel>();
            shell.OpenTool(tool.Id);
            if (shell.CurrentToolPage!.ToolViewModel is IAcceptsInput target)
            {
                target.AcceptInput("hello", null);
            }
            else
            {
                Assert.Equal("password-generator", tool.Id); // generates, has nothing to accept
            }
        });
    }

    [Fact]
    public void ClearingText_RemovesSuggestions()
    {
        var (services, shell) = Paste(Jwt);
        using var _ = services;

        shell.Home.SmartPaste.Text = "";
        shell.Home.SmartPaste.Analyze();

        Assert.Empty(shell.Home.SmartPaste.Suggestions);
        Assert.Null(shell.Home.SmartPaste.DetectedKind);
    }
}

using DevToolBox.App.Tools.EncodeDecode;
using DevToolBox.App.Tools.Html;
using DevToolBox.App.Tools.Url;
using DevToolBox.Core.Tools.EncodeDecode;
using DevToolBox.Core.Tools.Url;

namespace DevToolBox.App.Tests.Tools;

public class EncodeDecodeViewModelTests
{
    private static EncodeDecodeViewModel Create(string input)
    {
        var vm = new EncodeDecodeViewModel(TimeSpan.FromHours(1)) { Input = input };
        vm.Refresh();
        return vm;
    }

    [Fact]
    public void Auto_EncodesPlainText_AndDecodesBase64()
    {
        var encode = Create("Hello");
        Assert.Equal("SGVsbG8=", encode.Output);
        Assert.Equal("Auto: encoding", encode.DirectionText);

        var decode = Create("SGVsbG8=");
        Assert.Equal("Hello", decode.Output);
        Assert.Equal("Auto: decoding", decode.DirectionText);
    }

    [Fact]
    public void ChangingFormat_RecomputesImmediately()
    {
        var vm = Create("Hi");

        vm.SelectedEncoding = vm.Encodings.Single(e => e.Value == TextEncoding.Hex);

        Assert.Equal("4869", vm.Output);
    }

    [Fact]
    public void ForcedDecode_OfInvalidInput_ShowsError()
    {
        var vm = Create("not base64!");

        vm.Mode = CodecMode.Decode;

        Assert.Equal(string.Empty, vm.Output);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void Swap_MovesOutputToInput_AndFlipsDirection()
    {
        var vm = Create("Xin chào");

        vm.SwapCommand.Execute(null);

        Assert.Equal(CodecMode.Decode, vm.Mode);
        Assert.Equal("Xin chào", vm.Output);
    }

    [Fact]
    public void Clearing_Input_ClearsOutputAndError()
    {
        var vm = Create("x");
        vm.Mode = CodecMode.Decode;

        vm.Input = string.Empty;
        vm.Refresh();

        Assert.Equal(string.Empty, vm.Output);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void LoadSample_FillsInput() => Assert.Contains("DevToolBox", RunSample(new EncodeDecodeViewModel()), StringComparison.Ordinal);

    internal static string RunSample(DevToolBox.App.Tools.TextTransformViewModel vm)
    {
        vm.LoadSampleCommand.Execute(null);
        return vm.Input;
    }
}

public class HtmlViewModelTests
{
    [Fact]
    public void Encode_ThenSwap_DecodesBack()
    {
        var vm = new HtmlViewModel(TimeSpan.FromHours(1)) { Input = "<b>café</b>" };
        vm.Refresh();
        Assert.Equal("&lt;b&gt;café&lt;/b&gt;", vm.Output);

        vm.SelectedScope = vm.Scopes[1];
        Assert.Equal("&lt;b&gt;caf&eacute;&lt;/b&gt;", vm.Output);

        vm.SwapCommand.Execute(null);
        Assert.Equal(CodecDirection.Decode, vm.Direction);
        Assert.Equal("<b>café</b>", vm.Output);
        Assert.False(vm.IsEncoding);
    }

    [Fact]
    public void PreferNamedEntities_Off_UsesNumericReferences()
    {
        var vm = new HtmlViewModel(TimeSpan.FromHours(1)) { Input = "é" };
        vm.SelectedScope = vm.Scopes[1];

        vm.PreferNamedEntities = false;

        Assert.Equal("&#xE9;", vm.Output);
    }

    [Fact]
    public void Sample_DependsOnDirection()
    {
        var vm = new HtmlViewModel(TimeSpan.FromHours(1)) { Direction = CodecDirection.Decode };

        Assert.StartsWith("&lt;p", EncodeDecodeViewModelTests.RunSample(vm), StringComparison.Ordinal);
    }
}

public class UrlViewModelTests
{
    [Fact]
    public void Encode_ComponentVsFullUri()
    {
        var vm = new UrlViewModel(TimeSpan.FromHours(1)) { Input = "a b/c?d" };
        vm.Refresh();
        Assert.Equal("a%20b%2Fc%3Fd", vm.Output);

        vm.SelectedMode = vm.Modes.Single(m => m.Value == UrlEncodeMode.FullUri);
        Assert.Equal("a%20b/c?d", vm.Output);

        vm.PlusForSpace = true;
        Assert.Equal("a+b/c?d", vm.Output);
    }

    [Fact]
    public void Decode_MalformedEscape_ShowsError()
    {
        var vm = new UrlViewModel(TimeSpan.FromHours(1)) { Direction = CodecDirection.Decode, Input = "100%" };

        vm.Refresh();

        Assert.Contains("position 4", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_SplitsUrl_AndRebuildsAfterEditingParameters()
    {
        var vm = new UrlViewModel();

        vm.LoadSampleUrlCommand.Execute(null);

        Assert.True(vm.HasParsedUrl);
        Assert.Contains(vm.UrlParts, p => p.Label == "Host" && p.Value == "api.example.com");
        Assert.Equal(["q", "lang", "tags"], vm.QueryParameters.Select(p => p.Key));
        Assert.Equal("dev tools", vm.QueryParameters[0].Value);

        vm.QueryParameters[1].Value = "en";
        vm.QueryParameters[2].RemoveCommand!.Execute(null);
        vm.AddParameterCommand.Execute(null);
        vm.QueryParameters[^1].Key = "page";
        vm.QueryParameters[^1].Value = "2";

        Assert.Equal("https://user@api.example.com:8443/v1/search?q=dev%20tools&lang=en&page=2#results", vm.RebuiltUrl);
    }

    [Fact]
    public void Parser_InvalidUrl_ShowsError()
    {
        var vm = new UrlViewModel { UrlToParse = "example.com/no-scheme" };

        Assert.NotNull(vm.ParseError);
        Assert.False(vm.HasParsedUrl);
        Assert.Empty(vm.RebuiltUrl);
    }
}

using System.Text;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools.Barcode;
using DevToolBox.App.Tools.Hash;
using DevToolBox.App.Tools.ImageBase64;
using DevToolBox.App.Tools.Json;
using DevToolBox.App.Tools.Regex;
using DevToolBox.Core.Shared.Json;
using DevToolBox.Core.Tools.Barcode;
using DevToolBox.Core.Tools.Hash;

namespace DevToolBox.App.Tests.Tools;

public class JsonViewModelTests
{
    private static async Task<JsonViewModel> FormatAsync(string json)
    {
        var vm = new JsonViewModel(TimeSpan.FromHours(1)) { Input = json };
        await vm.FormatNowAsync();
        return vm;
    }

    [Fact]
    public async Task ValidJson_IsFormattedWithStatusAndTree()
    {
        using var vm = await FormatAsync("{\"b\":1,\"a\":[1,2]}");

        Assert.Equal("{\n  \"b\": 1,\n  \"a\": [\n    1,\n    2\n  ]\n}", vm.Output);
        Assert.StartsWith("Valid JSON · 5 values", vm.Status, StringComparison.Ordinal);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(["root", "b", "a", "[0]", "[1]"], vm.TreeRows.Select(r => r.Label));
    }

    [Fact]
    public async Task Options_ReformatWithoutRetyping()
    {
        using var vm = await FormatAsync("{\"b\":1,\"a\":2}");

        vm.SortKeys = true;
        await vm.LastRun;
        vm.SelectedIndent = vm.Indents.Single(i => i.Value == JsonIndent.Minified);
        await vm.LastRun;

        Assert.Equal("{\"a\":2,\"b\":1}", vm.Output);
    }

    [Fact]
    public async Task InvalidJson_ReportsLocation_AndKeepsLastGoodOutput()
    {
        using var vm = await FormatAsync("{\"a\":1}");

        vm.Input = "{\"a\":1,}";
        await vm.FormatNowAsync();

        Assert.StartsWith("Line 1, column 8", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, vm.ErrorLocation?.Line);
        Assert.Null(vm.Status);
        Assert.Contains("\"a\": 1", vm.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tree_ExpandAndCollapse()
    {
        using var vm = await FormatAsync("{\"deep\":{\"x\":{\"y\":1}}}");
        Assert.Equal(["root", "deep", "x"], vm.TreeRows.Select(r => r.Label));

        vm.ExpandAllCommand.Execute(null);
        Assert.Equal(["root", "deep", "x", "y"], vm.TreeRows.Select(r => r.Label));

        vm.CollapseAllCommand.Execute(null);
        Assert.Equal(["root", "deep"], vm.TreeRows.Select(r => r.Label));
        Assert.Equal("{…} 1 key", vm.TreeRows[1].ValueText);

        vm.TreeRows[1].ToggleCommand.Execute(null);
        Assert.Equal(["root", "deep", "x"], vm.TreeRows.Select(r => r.Label));
    }

    [Fact]
    public async Task EmptyInput_ClearsEverything()
    {
        using var vm = await FormatAsync("[1]");

        vm.Input = "  ";
        await vm.FormatNowAsync();

        Assert.Empty(vm.Output);
        Assert.Empty(vm.TreeRows);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task LargeJson_FormatsOffTheUiThread()
    {
        var json = "[" + string.Join(",", Enumerable.Range(0, 200_000).Select(i => $"{{\"id\":{i},\"v\":\"value {i}\"}}")) + "]";

        using var vm = await FormatAsync(json);

        Assert.StartsWith("Valid JSON · 600,001 values", vm.Status, StringComparison.Ordinal);
        Assert.Equal(200_001, vm.TreeRows.Count); // root + its items, each item collapsed
    }
}

public class RegexViewModelTests
{
    [Fact]
    public async Task Sample_FindsMatches_HighlightsAndReplaces()
    {
        using var vm = new RegexViewModel(TimeSpan.FromHours(1));
        vm.LoadSampleCommand.Execute(null);
        vm.ReplaceEnabled = true;

        await vm.RunNowAsync();

        Assert.Equal("2 matches", vm.Summary);
        Assert.Equal(["alice@example.com", "bob.smith+dev@mail.co.uk"], vm.Matches.Select(m => m.Value));
        Assert.Contains("user: alice", vm.Matches[0].Groups, StringComparison.Ordinal);
        Assert.Equal([(9, 17), (28, 24)], vm.Highlights.Select(h => (h.Start, h.Length)));
        Assert.StartsWith("Contact: alice at example.com", vm.ReplaceResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPattern_ShowsErrorWithPosition()
    {
        using var vm = new RegexViewModel(TimeSpan.FromHours(1)) { Pattern = "(abc", TestText = "abc" };

        await vm.RunNowAsync();

        Assert.Contains("at position 4", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(vm.Matches);
    }

    [Fact]
    public async Task Options_ChangeResults()
    {
        using var vm = new RegexViewModel(TimeSpan.FromHours(1)) { Pattern = "abc", TestText = "ABC abc" };
        await vm.RunNowAsync();
        Assert.Equal("1 match", vm.Summary);

        vm.IgnoreCase = true;
        await vm.RunNowAsync();
        Assert.Equal("2 matches", vm.Summary);

        vm.RightToLeft = true;
        await vm.RunNowAsync();
        Assert.Equal([0, 4], vm.Highlights.Select(h => h.Start));
    }

    [Fact]
    public async Task EmptyPattern_ClearsResults()
    {
        using var vm = new RegexViewModel(TimeSpan.FromHours(1)) { TestText = "abc" };

        await vm.RunNowAsync();

        Assert.Null(vm.Summary);
        Assert.Empty(vm.Matches);
    }
}

public class HashViewModelTests
{
    private readonly FakeFileService _files = new();

    [Fact]
    public void Text_ShowsAllSupportedAlgorithms()
    {
        using var vm = new HashViewModel(_files, TimeSpan.FromHours(1)) { TextInput = "abc" };

        vm.HashText();

        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Row(vm, HashAlgorithmKind.Md5).Value);
        Assert.Equal("352441c2", Row(vm, HashAlgorithmKind.Crc32).Value);
        Assert.Equal(HashAlgorithms.IsSupported(HashAlgorithmKind.Sha3_256), vm.Rows.Any(r => r.Algorithm == HashAlgorithmKind.Sha3_256));
        Assert.Equal(!HashAlgorithms.IsSupported(HashAlgorithmKind.Sha3_256), vm.UnsupportedNote is not null);
    }

    [Fact]
    public void FormatAndCompare()
    {
        using var vm = new HashViewModel(_files, TimeSpan.FromHours(1)) { TextInput = "abc" };
        vm.HashText();

        vm.SelectedFormat = vm.Formats.Single(f => f.Value == HashOutputFormat.HexUpper);
        Assert.Equal("900150983CD24FB0D6963F7D28E17F72", Row(vm, HashAlgorithmKind.Md5).Value);

        vm.ExpectedHash = "a9993e36 4706816a ba3e2571 7850c26c 9cd0d89d";
        Assert.True(Row(vm, HashAlgorithmKind.Sha1).IsMatch);
        Assert.True(vm.CompareMatched);
        Assert.Equal("✓ Matches SHA-1", vm.CompareResult);

        vm.ExpectedHash = "deadbeef";
        Assert.False(vm.CompareMatched);
    }

    [Fact]
    public void Hmac_UsesKey_AndSkipsCrc32()
    {
        using var vm = new HashViewModel(_files, TimeSpan.FromHours(1)) { TextInput = "what do ya want for nothing?", HmacKey = "Jefe" };

        vm.UseHmac = true;

        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", Row(vm, HashAlgorithmKind.Sha256).Value);
        Assert.StartsWith("—", Row(vm, HashAlgorithmKind.Crc32).Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_IsStreamedAndHashed()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 3_000_000));
        using var vm = new HashViewModel(_files, TimeSpan.FromHours(1));
        _files.FileToOpen = FakeFileService.InMemory("big.txt", data);

        await vm.OpenFileCommand.ExecuteAsync(null);
        await vm.LastFileRun;

        Assert.Equal(HashSource.File, vm.Source);
        Assert.Equal("big.txt", vm.FileName);
        Assert.False(vm.IsHashing);
        Assert.Equal(100, vm.Progress);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)), Row(vm, HashAlgorithmKind.Sha256).Value);
    }

    private static HashRowViewModel Row(HashViewModel vm, HashAlgorithmKind kind) => vm.Rows.Single(r => r.Algorithm == kind);
}

/// <summary>These tools create Avalonia bitmaps, so they run on the headless UI thread.</summary>
public class ImageToolViewModelTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeFileService _files = new();

    [AvaloniaFact]
    public void Barcode_RendersPreview_AndAddsCheckDigit()
    {
        using var vm = new BarcodeViewModel(_clipboard, _files, TimeSpan.FromHours(1));
        Assert.NotNull(vm.Preview);
        Assert.True(vm.IsQr);

        vm.SelectedKind = vm.Kinds.Single(k => k.Value == BarcodeKind.Ean13);

        Assert.Equal("400638133393", vm.Text);
        Assert.Equal("Encoded as 4006381333931 (check digit added)", vm.Info);
        Assert.True(vm.IsLinear);
        Assert.NotNull(vm.Preview);
    }

    [AvaloniaFact]
    public void Barcode_InvalidInput_ShowsErrorAndDisablesExport()
    {
        using var vm = new BarcodeViewModel(_clipboard, _files, TimeSpan.FromHours(1));
        vm.SelectedKind = vm.Kinds.Single(k => k.Value == BarcodeKind.Ean13);

        vm.Text = "4006381333932";
        vm.Render();

        Assert.Contains("check digit", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.False(vm.HasBarcode);
        Assert.Null(vm.Preview);
    }

    [AvaloniaFact]
    public async Task Barcode_ExportsSvgToFileAndClipboard()
    {
        using var vm = new BarcodeViewModel(_clipboard, _files, TimeSpan.FromHours(1));

        await vm.SaveSvgCommand.ExecuteAsync(null);
        await vm.CopySvgCommand.ExecuteAsync(null);

        Assert.Equal("qrcode.svg", _files.SavedName);
        Assert.StartsWith("<svg", Encoding.UTF8.GetString(_files.SavedBytes!), StringComparison.Ordinal);
        Assert.StartsWith("<svg", _clipboard.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Image_FromFile_ProducesDataUriAndSnippets()
    {
        using var vm = new ImageBase64ViewModel(_clipboard, _files, TimeSpan.FromHours(1));
        _files.FileToOpen = FakeFileService.InMemory("dot.png", Png);

        await vm.OpenImageCommand.ExecuteAsync(null);

        Assert.StartsWith("data:image/png;base64,iVBOR", vm.Output, StringComparison.Ordinal);
        Assert.StartsWith("dot.png · image/png · 68 B", vm.SourceInfo, StringComparison.Ordinal);
        vm.SelectedOutputKind = vm.OutputKinds.Single(k => k.Value == Base64OutputKind.HtmlImg);
        Assert.StartsWith("<img src=\"data:image/png", vm.Output, StringComparison.Ordinal);
        Assert.Null(vm.EncodeError);
    }

    [AvaloniaFact]
    public void Image_LargeOutput_IsTruncatedForDisplayOnly()
    {
        using var vm = new ImageBase64ViewModel(_clipboard, _files, TimeSpan.FromHours(1));
        var big = new byte[200_000];
        Png.CopyTo(big, 0);

        vm.LoadImage(big, "big.png");

        Assert.True(vm.Output.Length > ImageBase64ViewModel.MaxDisplayedChars);
        Assert.Contains("more characters", vm.OutputDisplay, StringComparison.Ordinal);
        Assert.StartsWith(vm.OutputDisplay[..1000], vm.Output, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Image_NotAnImage_ShowsError()
    {
        using var vm = new ImageBase64ViewModel(_clipboard, _files, TimeSpan.FromHours(1));

        vm.LoadImage("hello"u8.ToArray(), "notes.txt");

        Assert.NotNull(vm.EncodeError);
        Assert.Empty(vm.Output);
    }

    [AvaloniaFact]
    public async Task Base64ToImage_DecodesAndSaves()
    {
        using var vm = new ImageBase64ViewModel(_clipboard, _files, TimeSpan.FromHours(1))
        {
            Base64Input = "data:image/png;base64," + Convert.ToBase64String(Png),
        };

        vm.DecodeInput();
        await vm.SaveDecodedCommand.ExecuteAsync(null);

        Assert.True(vm.HasDecodedImage);
        Assert.StartsWith("image/png · 68 B", vm.DecodedInfo, StringComparison.Ordinal);
        Assert.Equal("image.png", _files.SavedName);
        Assert.Equal(Png, _files.SavedBytes);
    }

    [AvaloniaFact]
    public async Task PasteImage_WithDataUriText_FillsDecoder()
    {
        using var vm = new ImageBase64ViewModel(_clipboard, _files, TimeSpan.FromHours(1));
        _clipboard.Text = "data:image/png;base64," + Convert.ToBase64String(Png);

        await vm.PasteImageCommand.ExecuteAsync(null);

        Assert.Equal(_clipboard.Text, vm.Base64Input);
    }
}

using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools.EncodeDecode;
using DevToolBox.App.Tools.Json;
using DevToolBox.App.Tools.Password;
using DevToolBox.App.Tools.Regex;
using DevToolBox.Core.Tools.Password;

namespace DevToolBox.App.Tests.Tools;

public class PasswordRedesignTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void PassphraseMode_GeneratesWords()
    {
        var vm = new PasswordViewModel(_time) { WordCount = 4 };

        vm.Mode = PasswordMode.Passphrase;

        Assert.True(vm.IsPassphraseMode);
        Assert.Equal(4, vm.Current.Split(' ').Length);
        Assert.Contains("pool of 7,776 words", vm.StrengthText, StringComparison.Ordinal);
        Assert.Equal(PasswordStrength.Fair, vm.Strength);
    }

    [Fact]
    public void PassphraseOptions_SeparatorCapitalizeNumber()
    {
        var vm = new PasswordViewModel(_time) { Mode = PasswordMode.Passphrase };

        vm.SelectedSeparator = vm.Separators.Single(s => s.Value == ".");
        vm.Capitalize = true;
        vm.IncludeNumber = true;

        var words = vm.Current.Split('.');
        Assert.All(words, w => Assert.True(char.IsUpper(w[0])));
        Assert.Single(words, w => char.IsAsciiDigit(w[^1]));
    }

    [Fact]
    public void Regenerating_KeepsMaskedHistory_UntilCleared()
    {
        var vm = new PasswordViewModel(_time);
        var first = vm.Current;

        _time.Advance(TimeSpan.FromMinutes(2));
        vm.Generate();

        var entry = Assert.Single(vm.History);
        Assert.Equal(first, entry.Value);
        Assert.Equal(first[..3], entry.Masked[..3]);
        Assert.Contains('•', entry.Masked);
        Assert.Equal("2 minutes ago", entry.Ago);

        vm.ClearHistoryCommand.Execute(null);
        Assert.Empty(vm.History);
        Assert.False(vm.HasHistory);
    }

    [Fact]
    public void Current_IsColoredByCharacterClass_AndMeterHasFourSegments()
    {
        var vm = new PasswordViewModel(_time);

        Assert.Equal(vm.Current, string.Concat(vm.CurrentSegments.Select(s => s.Text)));
        Assert.All(vm.CurrentSegments.Where(s => s.BrushKey == "Brush.Json.Key"), s => Assert.All(s.Text, c => Assert.True(char.IsAsciiDigit(c))));
        Assert.Equal([true, true, true, true], vm.StrengthBars);
        Assert.Equal("Very strong", vm.StrengthLabel);
        Assert.False(vm.HasMany);

        vm.Count = 3;
        Assert.True(vm.HasMany);
    }
}

public class StatusBarTests
{
    [Fact]
    public async Task Json_ReportsKeysDepthAndSizes()
    {
        using var vm = new JsonViewModel(TimeSpan.FromHours(1)) { Input = "{\"a\":{\"b\":[1]},\"c\":2}" };

        await vm.FormatNowAsync();

        Assert.True(vm.IsValid);
        Assert.Equal("2 top-level keys · depth 3", vm.StatsText);
        Assert.StartsWith("21 B → ", vm.SizeText, StringComparison.Ordinal);

        vm.Indent = DevToolBox.Core.Shared.Json.JsonIndent.Minified;
        await vm.LastRun;
        Assert.Equal("{\"a\":{\"b\":[1]},\"c\":2}", vm.Output);
    }

    [Fact]
    public async Task Regex_ReportsMatchesGroupsAndTiming()
    {
        using var vm = new RegexViewModel(TimeSpan.FromHours(1)) { Pattern = @"(\d)(\d)", TestText = "12 34" };

        await vm.RunNowAsync();

        Assert.StartsWith("2 matches · 2 groups · ", vm.StatusText, StringComparison.Ordinal);
        Assert.EndsWith(" ms · .NET flavor", vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasMatches);
        Assert.NotEmpty(vm.QuickReference);
    }

    [Fact]
    public void EncodeDecode_ReportsSizes()
    {
        var vm = new EncodeDecodeViewModel(TimeSpan.FromHours(1)) { Input = "héllo" };

        vm.Refresh();

        Assert.Equal("6 B in → 8 chars out", vm.SizeText);
    }
}

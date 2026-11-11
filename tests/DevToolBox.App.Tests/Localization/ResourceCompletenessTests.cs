using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using DevToolBox.App.Localization;
using DevToolBox.Core.Localization;

namespace DevToolBox.App.Tests.Localization;

/// <summary>Every UI string exists in both languages, with the same placeholders, and every XAML key resolves.</summary>
public partial class ResourceCompletenessTests
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");

    public static TheoryData<string> ResourceSets => ["App", "Core"];

    [Theory]
    [MemberData(nameof(ResourceSets))]
    public void EveryString_IsTranslated_WithTheSamePlaceholders(string set)
    {
        var manager = Manager(set);
        var english = Entries(manager, CultureInfo.InvariantCulture);
        var vietnamese = Entries(manager, Vietnamese);

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(StringComparer.Ordinal), vietnamese.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, text) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(vietnamese[key]), $"{set}:{key} is empty in Vietnamese");
            Assert.True(Placeholders(text).SetEquals(Placeholders(vietnamese[key])), $"{set}:{key} placeholders differ");
        }
    }

    [Fact]
    public void EveryXamlKey_ExistsInTheResources()
    {
        var keys = Entries(Strings.ResourceManager, CultureInfo.InvariantCulture).Keys.ToHashSet(StringComparer.Ordinal);
        var views = Directory.GetFiles(Path.Combine(RepoRoot(), "src", "DevToolBox.App"), "*.axaml", SearchOption.AllDirectories);

        var used = views.SelectMany(f => TrKey().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: m.Groups[1].Value))).ToList();

        Assert.NotEmpty(used);
        Assert.Empty(used.Where(u => !keys.Contains(u.Key)).Select(u => $"{u.File}: {u.Key}"));
    }

    private static ResourceManager Manager(string set) => set == "App"
        ? Strings.ResourceManager
        : new ResourceManager("DevToolBox.Core.Localization.CoreStrings", typeof(CoreLocalization).Assembly);

    private static Dictionary<string, string> Entries(ResourceManager manager, CultureInfo culture) =>
        manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.Ordinal);

    private static HashSet<string> Placeholders(string text) =>
        [.. PlaceholderIndex().Matches(text).Select(m => m.Groups[1].Value)];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DevToolBox.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex PlaceholderIndex();

    [GeneratedRegex(@"\{l:Tr (\w+)\}")]
    private static partial Regex TrKey();
}

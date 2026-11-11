using Avalonia.Controls;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.TestDoubles;

internal sealed class FakeToolViewModel : ToolViewModelBase;

internal static class FakeTools
{
    public static ToolDefinition Create(string id, string name, ToolCategory category, params string[] keywords) =>
        new(id, () => name, () => name, () => $"{name} description", category, keywords, "XX", _ => new FakeToolViewModel(), () => new TextBlock { Text = name });

    public static IReadOnlyList<ToolDefinition> Catalog { get; } =
    [
        Create("jwt-decoder", "JWT Decoder", ToolCategory.EncodeDecode, "token", "jwt"),
        Create("timestamp", "Timestamp Converter", ToolCategory.Convert, "epoch", "unix", "date"),
        Create("uuid", "UUID Generator", ToolCategory.Generate, "guid"),
    ];
}

internal sealed class FakeToolViewModelFactory : IToolViewModelFactory
{
    public int CreatedCount { get; private set; }

    public ToolViewModelBase Create(ToolDefinition tool)
    {
        CreatedCount++;
        return tool.CreateViewModel(null!);
    }
}

internal sealed class FakeThemeService : DevToolBox.App.Platform.IThemeService
{
    public DevToolBox.App.Platform.ThemePreference Preference { get; private set; }

    public event EventHandler? PreferenceChanged;

    public void ApplySavedPreference()
    {
    }

    public void SetPreference(DevToolBox.App.Platform.ThemePreference preference)
    {
        Preference = preference;
        PreferenceChanged?.Invoke(this, EventArgs.Empty);
    }
}

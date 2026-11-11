using Avalonia.Controls;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools;

/// <summary>Tool groups from the DevToolBox design, in sidebar order.</summary>
public enum ToolCategory
{
    Convert,
    EncodeDecode,
    FormatAndTest,
    Generate,
}

public static class ToolCategoryNames
{
    /// <summary>Sidebar group label.</summary>
    public static string DisplayName(this ToolCategory category) => category switch
    {
        ToolCategory.Convert => Strings.Category_Convert,
        ToolCategory.EncodeDecode => Strings.Category_EncodeDecode,
        ToolCategory.FormatAndTest => Strings.Category_FormatAndTest,
        _ => Strings.Category_Generate,
    };

    /// <summary>Short label for Home filter chips and card captions.</summary>
    public static string ShortName(this ToolCategory category) => category switch
    {
        ToolCategory.Convert => Strings.Category_Convert,
        ToolCategory.EncodeDecode => Strings.Category_EncodeShort,
        ToolCategory.FormatAndTest => Strings.Category_FormatAndTest,
        _ => Strings.Category_Generate,
    };
}

/// <summary>
/// Everything the shell needs to list, search and open a tool. Views and view models are created
/// lazily on first navigation. Display text is read from the localized resources each time, so it follows
/// the current UI language.
/// </summary>
/// <param name="Id">Stable kebab-case id, e.g. "jwt-decoder". Persisted in settings (favorites).</param>
/// <param name="GetName">Full name, shown on Home cards and as the page title.</param>
/// <param name="GetShortName">Compact name for the sidebar.</param>
/// <param name="GetDescription">One sentence shown on the Home card and under the page title.</param>
/// <param name="Keywords">Extra English search terms (names are searched in the current language).</param>
/// <param name="Badge">2–4 character monospace badge, e.g. "JWT" or "{}".</param>
public sealed record ToolDefinition(
    string Id,
    Func<string> GetName,
    Func<string> GetShortName,
    Func<string> GetDescription,
    ToolCategory Category,
    IReadOnlyList<string> Keywords,
    string Badge,
    Func<IServiceProvider, ToolViewModelBase> CreateViewModel,
    Func<Control> CreateView)
{
    public string Name => GetName();

    public string ShortName => GetShortName();

    public string Description => GetDescription();
}

using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.Core.Shared.Json;

namespace DevToolBox.App.Tools.Json;

/// <summary>One visible row of the flattened, virtualized JSON tree.</summary>
public sealed class JsonTreeRowViewModel(JsonTreeNode node, string label, int depth, bool isExpanded, IRelayCommand toggle)
{
    private const int IndentPerLevel = 16;

    public JsonTreeNode Node { get; } = node;

    public string Label { get; } = label;

    public double IndentWidth { get; } = depth * IndentPerLevel;

    public bool IsExpandable => Node.IsContainer && Node.ChildCount > 0;

    public bool IsExpanded { get; } = isExpanded;

    public string ChevronIcon => IsExpanded ? "ChevronDown" : "ChevronRight";

    public IRelayCommand ToggleCommand { get; } = toggle;

    public string ValueText => Node.Kind switch
    {
        JsonNodeKind.Object => IsExpanded ? "{" : "{…} " + Localizer.Format(Node.ChildCount == 1 ? Strings.Json_Key : Strings.Json_Keys, Node.ChildCount),
        JsonNodeKind.Array => IsExpanded ? "[" : "[…] " + Localizer.Format(Node.ChildCount == 1 ? Strings.Json_Item : Strings.Json_Items, Node.ChildCount),
        _ => Node.RawValue ?? string.Empty,
    };

    public string KindClass => Node.Kind switch
    {
        JsonNodeKind.String => "json-string",
        JsonNodeKind.Number => "json-number",
        JsonNodeKind.True or JsonNodeKind.False or JsonNodeKind.Null => "json-keyword",
        _ => "json-punct",
    };
}

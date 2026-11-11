using Avalonia.Data.Converters;

namespace DevToolBox.App.Tools.Json;

/// <summary>Maps a row's KindClass to a bool for one style class (json-string, json-number…).</summary>
public static class JsonKindClassConverter
{
    public static readonly IValueConverter String = For("json-string");
    public static readonly IValueConverter Number = For("json-number");
    public static readonly IValueConverter Keyword = For("json-keyword");

    private static FuncValueConverter<string?, bool> For(string kind) => new(value => value == kind);
}

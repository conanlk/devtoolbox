using Avalonia.Data.Converters;

namespace DevToolBox.App.Shared.Converters;

/// <summary>True when a bound integer (e.g. a selected tab index) equals a fixed value.</summary>
public static class IntEqualsConverter
{
    public static readonly IValueConverter Zero = For(0);
    public static readonly IValueConverter One = For(1);

    private static FuncValueConverter<int, bool> For(int expected) => new(value => value == expected);
}

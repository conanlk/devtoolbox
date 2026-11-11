using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DevToolBox.App.Shared.Converters;

/// <summary>
/// Binds a RadioButton to an enum value: <c>IsChecked="{Binding Mode, Converter={x:Static conv:EnumEqualsConverter.Instance}, ConverterParameter=Decode}"</c>.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string name && targetType.IsEnum
            ? Enum.Parse(targetType, name)
            : BindingOperations.DoNothing;
}

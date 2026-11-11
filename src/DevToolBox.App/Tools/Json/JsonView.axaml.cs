using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevToolBox.App.Tools.Json;

public partial class JsonView : UserControl
{
    public JsonView() => InitializeComponent();

    private void OnGoToErrorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JsonViewModel { ErrorLocation: { Line: { } line, Column: { } column } })
        {
            InputEditor.GoTo(line, column);
        }
    }
}

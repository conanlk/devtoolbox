using Avalonia;
using Avalonia.Controls;

namespace DevToolBox.App.Tools.AppIcons;

public partial class AppIconsView : UserControl
{
    /// <summary>Below this width the preview/output column moves under source/options.</summary>
    private const double StackedBelowWidth = 880;

    private bool? _stacked;

    public AppIconsView() => InitializeComponent();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var stacked = e.NewSize.Width < StackedBelowWidth;
        if (stacked == _stacked)
        {
            return;
        }

        _stacked = stacked;
        Columns.ColumnDefinitions = ColumnDefinitions.Parse(stacked ? "*" : "*,16,1.7*");
        Grid.SetColumn(RightColumn, stacked ? 0 : 2);
        Grid.SetRow(RightColumn, stacked ? 1 : 0);
        RightColumn.Margin = stacked ? new Thickness(0, 16, 0, 0) : default;
    }
}

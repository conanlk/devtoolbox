using Avalonia;
using Avalonia.Controls;

namespace DevToolBox.App.Shared.Controls;

/// <summary>
/// CSS-like <c>repeat(auto-fill, minmax(MinItemWidth, 1fr))</c>: as many equal columns as fit, items
/// stretched to fill the row. Rows take the tallest item's height.
/// </summary>
public sealed class AdaptiveGridPanel : Panel
{
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(MinItemWidth), 250);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(Spacing), 12);

    static AdaptiveGridPanel() => AffectsMeasure<AdaptiveGridPanel>(MinItemWidthProperty, SpacingProperty);

    public double MinItemWidth
    {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, itemWidth) = Layout(availableSize.Width);
        var height = 0.0;
        var rowHeight = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            if ((i + 1) % columns == 0 || i == Children.Count - 1)
            {
                height += rowHeight + (i == Children.Count - 1 ? 0 : Spacing);
                rowHeight = 0;
            }
        }

        var width = double.IsInfinity(availableSize.Width) ? (columns * (itemWidth + Spacing)) - Spacing : availableSize.Width;
        return new Size(Math.Max(0, width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, itemWidth) = Layout(finalSize.Width);
        var y = 0.0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            var row = Children.Skip(start).Take(columns).ToList();
            var rowHeight = row.Max(c => c.DesiredSize.Height);
            for (var c = 0; c < row.Count; c++)
            {
                row[c].Arrange(new Rect(c * (itemWidth + Spacing), y, itemWidth, rowHeight));
            }

            y += rowHeight + Spacing;
        }

        return finalSize;
    }

    private (int Columns, double ItemWidth) Layout(double width)
    {
        if (double.IsInfinity(width) || width <= 0)
        {
            return (Math.Max(1, Children.Count), MinItemWidth);
        }

        var columns = Math.Max(1, (int)((width + Spacing) / (MinItemWidth + Spacing)));
        return (columns, (width - ((columns - 1) * Spacing)) / columns);
    }
}

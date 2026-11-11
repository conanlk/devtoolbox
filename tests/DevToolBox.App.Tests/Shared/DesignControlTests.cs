using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Shared.Controls;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Shared;

public class DesignControlTests
{
    private static Window Show(Control content, double width = 800)
    {
        var window = new Window { Content = content, Width = width, Height = 600 };
        window.Show();
        UiHelpers.Settle();
        return window;
    }

    [AvaloniaTheory]
    [InlineData(800, 3)]
    [InlineData(500, 1)]
    [InlineData(1100, 4)]
    public void AdaptiveGrid_FitsAsManyColumnsAsAllowed(double width, int expectedColumns)
    {
        var panel = new AdaptiveGridPanel { MinItemWidth = 250, Spacing = 12 };
        for (var i = 0; i < 6; i++)
        {
            panel.Children.Add(new Border { Height = 50 });
        }

        Show(panel, width);

        var firstRowTop = panel.Children[0].Bounds.Top;
        Assert.Equal(expectedColumns, panel.Children.Count(c => c.Bounds.Top == firstRowTop));
        Assert.True(panel.Children[0].Bounds.Width >= 250);
    }

    [AvaloniaFact]
    public void ToolPanel_ShowsUppercaseHeader_AndHidesStripWithoutOne()
    {
        var withHeader = new ToolPanel { Header = "Input", Content = new TextBlock() };
        var withoutHeader = new ToolPanel { Content = new TextBlock() };
        Show(new StackPanel { Children = { withHeader, withoutHeader } });

        Assert.Equal("INPUT", withHeader.HeaderText);
        Assert.True(withHeader.HasHeader);
        Assert.False(withoutHeader.HasHeader);
    }

    [AvaloniaFact]
    public void SegmentedText_BuildsColoredRuns()
    {
        var text = new SegmentedText { Segments = [new("ab", null), new("12", "Brush.Json.Key")] };
        Show(text);

        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["ab", "12"], runs.Select(r => r.Text));
        Assert.NotNull(runs[1].Foreground);
    }

    [AvaloniaFact]
    public void CodeEditor_ReportsCaretPosition()
    {
        var editor = new CodeEditor { Height = 200 };
        Show(editor);

        editor.Text = "line one\nline two";
        editor.GoTo(2, 5);

        Assert.Equal("Ln 2, Col 5", editor.CaretText);
    }
}

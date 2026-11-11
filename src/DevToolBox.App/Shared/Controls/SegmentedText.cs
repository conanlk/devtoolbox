using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DevToolBox.App.Shared.Controls;

/// <summary>A run of text with a color role, e.g. a JWT part or a password character class.</summary>
/// <param name="BrushKey">Theme resource key of the foreground brush, or null for the default.</param>
public readonly record struct TextSegment(string Text, string? BrushKey);

/// <summary>Selectable text built from colored segments; colors follow the active theme.</summary>
public sealed class SegmentedText : SelectableTextBlock
{
    public static readonly StyledProperty<IReadOnlyList<TextSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<SegmentedText, IReadOnlyList<TextSegment>?>(nameof(Segments));

    public SegmentedText() => ActualThemeVariantChanged += (_, _) => Rebuild();

    public IReadOnlyList<TextSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
        {
            Rebuild();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        foreach (var segment in Segments ?? [])
        {
            var run = new Run(segment.Text);
            if (segment.BrushKey is { } key && this.TryFindResource(key, ActualThemeVariant, out var brush) && brush is IBrush b)
            {
                run.Foreground = b;
            }

            inlines.Add(run);
        }

        Inlines = inlines;
    }
}

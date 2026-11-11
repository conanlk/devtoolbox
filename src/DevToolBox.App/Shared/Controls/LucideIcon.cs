using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DevToolBox.App.Shared.Controls;

/// <summary>
/// Draws a Lucide icon (24×24 stroke geometry) scaled to the control's size, using the inherited
/// foreground as stroke. Set <see cref="Kind"/> to an icon resource name ("Star" → "Icon.Star").
/// </summary>
public sealed class LucideIcon : Control
{
    private const double CanvasSize = 24;
    private const double DefaultSize = 16;

    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<LucideIcon, string?>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LucideIcon>();

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LucideIcon, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LucideIcon, double>(nameof(StrokeThickness), 2);

    private Geometry? _geometry;

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(ForegroundProperty, FillProperty, StrokeThicknessProperty);
        KindProperty.Changed.AddClassHandler<LucideIcon>((icon, _) => icon.ResolveGeometry());
    }

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Optional fill, e.g. a filled star for favorites.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResolveGeometry();
    }

    protected override Size MeasureOverride(Size availableSize) => new(DefaultSize, DefaultSize);

    public override void Render(DrawingContext context)
    {
        if (_geometry is null)
        {
            return;
        }

        var size = Math.Min(Bounds.Width, Bounds.Height);
        var scale = size / CanvasSize;
        var offsetX = (Bounds.Width - size) / 2;
        var offsetY = (Bounds.Height - size) / 2;
        var pen = new Pen(Foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(Fill, pen, _geometry);
        }
    }

    private void ResolveGeometry()
    {
        _geometry = Kind is { Length: > 0 } kind && this.TryFindResource("Icon." + kind, out var resource)
            ? resource as Geometry
            : null;
        InvalidateVisual();
    }
}

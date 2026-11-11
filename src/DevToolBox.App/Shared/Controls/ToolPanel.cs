using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DevToolBox.App.Shared.Controls;

/// <summary>
/// A bordered section from the DevToolBox design: an uppercase label strip (with optional actions on the
/// right) above the content. Header text is shown uppercase; set <see cref="HeaderBrush"/> to tint it.
/// </summary>
public class ToolPanel : ContentControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<ToolPanel, string?>(nameof(Header));

    public static readonly StyledProperty<IBrush?> HeaderBrushProperty =
        AvaloniaProperty.Register<ToolPanel, IBrush?>(nameof(HeaderBrush));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ToolPanel, object?>(nameof(Actions));

    /// <summary>Right by default; Stretch lets the actions fill the strip (e.g. tabs on the left, buttons on the right).</summary>
    public static readonly StyledProperty<HorizontalAlignment> ActionsAlignmentProperty =
        AvaloniaProperty.Register<ToolPanel, HorizontalAlignment>(nameof(ActionsAlignment), HorizontalAlignment.Right);

    public static readonly DirectProperty<ToolPanel, string?> HeaderTextProperty =
        AvaloniaProperty.RegisterDirect<ToolPanel, string?>(nameof(HeaderText), p => p.HeaderText);

    public static readonly DirectProperty<ToolPanel, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<ToolPanel, bool>(nameof(HasHeader), p => p.HasHeader);

    private string? _headerText;
    private bool _hasHeader;

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IBrush? HeaderBrush
    {
        get => GetValue(HeaderBrushProperty);
        set => SetValue(HeaderBrushProperty, value);
    }

    /// <summary>Controls shown at the right of the header strip (buttons, counters, legends).</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public HorizontalAlignment ActionsAlignment
    {
        get => GetValue(ActionsAlignmentProperty);
        set => SetValue(ActionsAlignmentProperty, value);
    }

    public string? HeaderText
    {
        get => _headerText;
        private set => SetAndRaise(HeaderTextProperty, ref _headerText, value);
    }

    public bool HasHeader
    {
        get => _hasHeader;
        private set => SetAndRaise(HasHeaderProperty, ref _hasHeader, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == ActionsProperty)
        {
            HeaderText = Header?.ToUpperInvariant();
            HasHeader = !string.IsNullOrEmpty(Header) || Actions is not null;
        }
    }
}

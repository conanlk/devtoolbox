using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Shared.Controls;

/// <summary>
/// Base for small icon + label toolbar buttons that use the Fluent button theme. The label is read through a
/// delegate so built-in labels (Copy, Paste, Clear) follow the UI language.
/// </summary>
public abstract class ToolbarButton : Button, ILanguageAware
{
    private readonly LucideIcon _icon = new() { Width = 14, Height = 14 };
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private Func<string> _labelText = () => string.Empty;
    private Func<string> _automationName = () => string.Empty;

    protected ToolbarButton(string icon, Func<string> label)
    {
        Classes.Add("tool");
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _icon, _label } };
        _automationName = label;
        ShowState(icon, label);
        Localizer.Register(this);
    }

    /// <summary>The button's accessible name; it stays put while a transient state ("Copied") is shown.</summary>
    protected void SetAutomationName(Func<string> name)
    {
        _automationName = name;
        ShowLabel();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    protected void ShowState(string icon, Func<string> label)
    {
        _icon.Kind = icon;
        _labelText = label;
        ShowLabel();
    }

    void ILanguageAware.OnLanguageChanged() => ShowLabel();

    private void ShowLabel()
    {
        _label.Text = _labelText();
        Avalonia.Automation.AutomationProperties.SetName(this, _automationName());
    }
}

/// <summary>Copies <see cref="Text"/> to the clipboard and briefly shows "Copied".</summary>
public sealed class CopyButton : ToolbarButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Text));

    private IDisposable? _resetTimer;

    public CopyButton()
        : base("Copy", () => Strings.Common_Copy)
    {
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            IsEnabled = !string.IsNullOrEmpty(Text);
        }
    }

    protected override async void OnClick()
    {
        base.OnClick();
        if (string.IsNullOrEmpty(Text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(Text);
        ShowState("Check", () => Strings.Common_Copied);
        _resetTimer?.Dispose();
        _resetTimer = DispatcherTimer.RunOnce(() => ShowState("Copy", () => Strings.Common_Copy), TimeSpan.FromSeconds(1.5));
    }
}

/// <summary>Replaces the bound <see cref="Text"/> with the clipboard's text.</summary>
public sealed class PasteButton : ToolbarButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PasteButton, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public PasteButton()
        : base("ClipboardPaste", () => Strings.Common_Paste)
    {
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override async void OnClick()
    {
        base.OnClick();
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && await clipboard.TryGetTextAsync() is { } text)
        {
            SetCurrentValue(TextProperty, text);
        }
    }
}

/// <summary>Clears the bound <see cref="Text"/>.</summary>
public sealed class ClearButton : ToolbarButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ClearButton, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public ClearButton()
        : base("Eraser", () => Strings.Common_Clear)
    {
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        SetCurrentValue(TextProperty, string.Empty);
    }
}

/// <summary>Toolbar button with an icon and label, for view-model commands (Sample, Swap, Generate…).</summary>
public sealed class CommandButton : ToolbarButton
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<CommandButton, string>(nameof(Icon), "Check");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CommandButton, string>(nameof(Label), string.Empty);

    public CommandButton()
        : base("Check", () => string.Empty)
    {
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == LabelProperty)
        {
            ShowState(Icon, () => Label);
            SetAutomationName(() => Label);
        }
    }
}

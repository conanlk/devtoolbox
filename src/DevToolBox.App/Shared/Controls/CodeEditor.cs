using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Shared.Controls;

public enum CodeLanguage
{
    PlainText,
    Json,
}

/// <summary>A highlighted range in the document (e.g. a regex match). Index picks the alternating color.</summary>
public readonly record struct HighlightSpan(int Start, int Length, int Index);

/// <summary>
/// AvaloniaEdit wrapped with bindable <see cref="Text"/>, monospace font, theme-aware JSON coloring and
/// match highlighting. Handles multi-megabyte documents with line virtualization.
/// </summary>
public sealed class CodeEditor : UserControl, ILanguageAware
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CodeEditor, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<CodeLanguage> LanguageProperty =
        AvaloniaProperty.Register<CodeEditor, CodeLanguage>(nameof(Language));

    public static readonly StyledProperty<IReadOnlyList<HighlightSpan>?> HighlightsProperty =
        AvaloniaProperty.Register<CodeEditor, IReadOnlyList<HighlightSpan>?>(nameof(Highlights));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<CodeEditor, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(WordWrap));

    public static readonly DirectProperty<CodeEditor, string> CaretTextProperty =
        AvaloniaProperty.RegisterDirect<CodeEditor, string>(nameof(CaretText), e => e.CaretText);

    private readonly TextEditor _editor;
    private string _caretText = string.Empty;
    private readonly JsonColorizer _jsonColorizer = new();
    private readonly SpanHighlighter _spanHighlighter = new();
    private bool _isSyncing;

    public CodeEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            FontSize = 13,
        };
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 2;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.TextView.LineTransformers.Add(_spanHighlighter);
        _editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretText();
        UpdateCaretText();
        Localizer.Register(this);
        Content = _editor;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
    }

    void ILanguageAware.OnLanguageChanged() => UpdateCaretText();

    private void UpdateCaretText() =>
        CaretText = Localizer.Format(Strings.Common_CaretPosition, _editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public CodeLanguage Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public IReadOnlyList<HighlightSpan>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool WordWrap
    {
        get => GetValue(WordWrapProperty);
        set => SetValue(WordWrapProperty, value);
    }

    /// <summary>"Ln 3, Col 14" — for status bars.</summary>
    public string CaretText
    {
        get => _caretText;
        private set => SetAndRaise(CaretTextProperty, ref _caretText, value);
    }

    /// <summary>The underlying editor, for focus and selection in views and tests.</summary>
    public TextEditor Editor => _editor;

    /// <summary>Moves the caret to a 1-based line/column, scrolls it into view and focuses the editor.</summary>
    public void GoTo(int line, int column)
    {
        var document = _editor.Document;
        if (document.LineCount == 0)
        {
            return;
        }

        var targetLine = document.GetLineByNumber(Math.Clamp(line, 1, document.LineCount));
        _editor.CaretOffset = targetLine.Offset + Math.Clamp(column - 1, 0, targetLine.Length);
        _editor.ScrollTo(targetLine.LineNumber, column);
        _editor.Focus();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (this.TryFindResource("Font.Mono", out var font) && font is FontFamily family)
        {
            _editor.FontFamily = family;
        }

        ApplyTheme();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            SyncTextToEditor(change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _editor.IsReadOnly = IsReadOnly;
        }
        else if (change.Property == LanguageProperty)
        {
            ApplyLanguage();
        }
        else if (change.Property == HighlightsProperty)
        {
            _spanHighlighter.Spans = Highlights ?? [];
            _editor.TextArea.TextView.Redraw();
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            _editor.Watermark = PlaceholderText;
        }
        else if (change.Property == WordWrapProperty)
        {
            _editor.WordWrap = WordWrap;
        }
    }

    private void SyncTextToEditor(string text)
    {
        if (_isSyncing || _editor.Document.TextLength == text.Length && _editor.Text == text)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            _editor.Document.Text = text;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            SetCurrentValue(TextProperty, _editor.Text);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ApplyLanguage()
    {
        var transformers = _editor.TextArea.TextView.LineTransformers;
        transformers.Remove(_jsonColorizer);
        if (Language == CodeLanguage.Json)
        {
            transformers.Insert(0, _jsonColorizer);
        }

        _editor.TextArea.TextView.Redraw();
    }

    private void ApplyTheme()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        _jsonColorizer.Palette = dark ? JsonPalette.Dark : JsonPalette.Light;
        _spanHighlighter.Brushes = dark ? SpanHighlighter.DarkBrushes : SpanHighlighter.LightBrushes;
        _editor.TextArea.TextView.Redraw();
    }
}

using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace DevToolBox.App.Shared.Controls;

public sealed record JsonPalette(IBrush Key, IBrush String, IBrush Number, IBrush Keyword)
{
    public static readonly JsonPalette Dark = new(Brush("#9CDCFE"), Brush("#CE9178"), Brush("#B5CEA8"), Brush("#569CD6"));
    public static readonly JsonPalette Light = new(Brush("#0451A5"), Brush("#A31515"), Brush("#098658"), Brush("#0000FF"));

    private static ImmutableSolidColorBrush Brush(string hex) => new(Color.Parse(hex));
}

/// <summary>
/// Single-line JSON tokenizer for coloring visible lines only. Very long lines (minified megabytes)
/// are left uncolored to keep rendering fast.
/// </summary>
public sealed class JsonColorizer : DocumentColorizingTransformer
{
    private const int MaxColorizedLineLength = 20_000;

    public JsonPalette Palette { get; set; } = JsonPalette.Dark;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length is 0 or > MaxColorizedLineLength)
        {
            return;
        }

        var text = CurrentContext.Document.GetText(line);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"')
            {
                var end = SkipString(text, i);
                var next = end;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                {
                    next++;
                }

                Paint(line, i, end, next < text.Length && text[next] == ':' ? Palette.Key : Palette.String);
                i = end;
            }
            else if (c == '-' || char.IsAsciiDigit(c))
            {
                var end = i + 1;
                while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    end++;
                }

                Paint(line, i, end, Palette.Number);
                i = end;
            }
            else if (StartsWithWord(text, i, "true") || StartsWithWord(text, i, "null"))
            {
                Paint(line, i, i + 4, Palette.Keyword);
                i += 4;
            }
            else if (StartsWithWord(text, i, "false"))
            {
                Paint(line, i, i + 5, Palette.Keyword);
                i += 5;
            }
            else
            {
                i++;
            }
        }
    }

    private static int SkipString(string text, int start)
    {
        var i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == '"')
            {
                return i + 1;
            }

            i++;
        }

        return text.Length;
    }

    private static bool StartsWithWord(string text, int index, string word) =>
        string.CompareOrdinal(text, index, word, 0, word.Length) == 0;

    private void Paint(DocumentLine line, int start, int end, IBrush brush) =>
        ChangeLinePart(line.Offset + start, line.Offset + Math.Min(end, line.Length), e => e.TextRunProperties.SetForegroundBrush(brush));
}

/// <summary>Paints background spans (regex matches) with alternating colors.</summary>
public sealed class SpanHighlighter : DocumentColorizingTransformer
{
    // DevToolBox design: matches are amber "marks"; neighbours alternate in lightness so adjacent matches stay distinct.
    public static readonly IBrush[] DarkBrushes = [new ImmutableSolidColorBrush(Color.Parse("#4A3A14")), new ImmutableSolidColorBrush(Color.Parse("#5E4A19"))];
    public static readonly IBrush[] LightBrushes = [new ImmutableSolidColorBrush(Color.Parse("#FCE7B0")), new ImmutableSolidColorBrush(Color.Parse("#F6D27A"))];

    public IReadOnlyList<HighlightSpan> Spans { get; set; } = [];

    public IBrush[] Brushes { get; set; } = DarkBrushes;

    protected override void ColorizeLine(DocumentLine line)
    {
        var spans = Spans;
        if (spans.Count == 0)
        {
            return;
        }

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        for (var i = FirstEndingAfter(spans, lineStart); i < spans.Count && spans[i].Start < lineEnd; i++)
        {
            var span = spans[i];
            var start = Math.Max(span.Start, lineStart);
            var end = Math.Min(span.Start + span.Length, lineEnd);
            if (end > start)
            {
                var brush = Brushes[span.Index % Brushes.Length];
                ChangeLinePart(start, end, e => e.TextRunProperties.SetBackgroundBrush(brush));
            }
        }
    }

    /// <summary>Binary search: spans are sorted by start and don't overlap.</summary>
    private static int FirstEndingAfter(IReadOnlyList<HighlightSpan> spans, int offset)
    {
        int low = 0, high = spans.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (spans[mid].Start + spans[mid].Length <= offset)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}

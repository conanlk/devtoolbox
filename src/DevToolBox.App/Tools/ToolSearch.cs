using System.Globalization;
using System.Text;

namespace DevToolBox.App.Tools;

public static class ToolSearch
{
    /// <summary>
    /// True when every whitespace-separated term appears in the tool's name, short name or keywords
    /// (case- and accent-insensitive, so "ma hoa" finds "Mã hóa"). An empty query matches everything.
    /// </summary>
    public static bool Matches(ToolDefinition tool, string? query)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Fold).ToList();
        var haystack = new[] { tool.Name, tool.ShortName }.Concat(tool.Keywords).Select(Fold).ToList();
        return terms.All(term => haystack.Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Strips diacritics (and maps Vietnamese đ to d) for accent-insensitive matching.</summary>
    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c switch
                {
                    'đ' => 'd',
                    'Đ' => 'D',
                    _ => c,
                });
            }
        }

        return builder.ToString();
    }
}

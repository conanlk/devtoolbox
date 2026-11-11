using System.Globalization;
using System.Net;
using System.Text;

namespace DevToolBox.Core.Tools.Html;

public enum HtmlEncodeScope
{
    /// <summary>Only characters that are unsafe in HTML text and attributes: &amp; &lt; &gt; " '.</summary>
    UnsafeOnly,

    /// <summary>Unsafe characters plus every non-ASCII character.</summary>
    AllNonAscii,
}

public static class HtmlEntityCodec
{
    public static string Encode(string? text, HtmlEncodeScope scope, bool preferNamedEntities)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length + 16);
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                case '\'':
                    builder.Append("&#39;");
                    break;
                case > 127 when scope == HtmlEncodeScope.AllNonAscii:
                    AppendNonAscii(builder, rune.Value, preferNamedEntities);
                    break;
                default:
                    builder.Append(rune.ToString());
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes named (HTML 4 set) and numeric (decimal and hex) character references.</summary>
    public static string Decode(string? text) => WebUtility.HtmlDecode(text ?? string.Empty);

    private static void AppendNonAscii(StringBuilder builder, int codePoint, bool preferNamed)
    {
        if (preferNamed && HtmlNamedEntities.TryGetName(codePoint, out var name))
        {
            builder.Append('&').Append(name).Append(';');
        }
        else
        {
            builder.Append("&#x").Append(codePoint.ToString("X", CultureInfo.InvariantCulture)).Append(';');
        }
    }
}

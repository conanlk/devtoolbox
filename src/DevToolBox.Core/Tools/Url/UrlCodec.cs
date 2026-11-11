using System.Globalization;
using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Codecs;

namespace DevToolBox.Core.Tools.Url;

public enum UrlEncodeMode
{
    /// <summary>JavaScript encodeURIComponent: for a single query value or path segment.</summary>
    Component,

    /// <summary>JavaScript encodeURI: for a whole URL; keeps ; , / ? : @ &amp; = + $ #.</summary>
    FullUri,
}

/// <summary>Percent-encoding with the exact semantics of JavaScript's encodeURI(Component).</summary>
public static class UrlCodec
{
    private const string ComponentSafe = "-_.!~*'()";
    private const string UriReserved = ";,/?:@&=+$#";

    public static Result<string> Encode(string? text, UrlEncodeMode mode, bool spaceAsPlus = false)
    {
        text ??= string.Empty;
        var builder = new StringBuilder(text.Length + 16);
        Span<byte> utf8 = stackalloc byte[4];

        for (var i = 0; i < text.Length; i++)
        {
            if (!Rune.TryGetRuneAt(text, i, out var rune))
            {
                return new Error("url.lone-surrogate", CoreLocalization.Format(CoreStrings.Url_LoneSurrogate, i + 1))
                {
                    Location = new ErrorLocation(Offset: i),
                };
            }

            if (rune.IsBmp && IsUnescaped((char)rune.Value, mode))
            {
                builder.Append((char)rune.Value);
            }
            else if (rune.Value == ' ' && spaceAsPlus)
            {
                builder.Append('+');
            }
            else
            {
                var length = rune.EncodeToUtf8(utf8);
                foreach (var b in utf8[..length])
                {
                    builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            i += rune.Utf16SequenceLength - 1;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Decodes %XX sequences (UTF-8). In <see cref="UrlEncodeMode.FullUri"/> mode, escapes of reserved
    /// characters are kept, like JavaScript's decodeURI.
    /// </summary>
    public static Result<string> Decode(string? text, UrlEncodeMode mode, bool plusAsSpace = false)
    {
        text ??= string.Empty;
        var output = new StringBuilder(text.Length);
        var pending = new List<byte>();
        var pendingStart = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '%')
            {
                if (i + 2 >= text.Length || !byte.TryParse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    return new Error("url.malformed-escape", CoreLocalization.Format(CoreStrings.Url_MalformedEscape, i + 1))
                    {
                        Location = new ErrorLocation(Offset: i),
                    };
                }

                if (mode == UrlEncodeMode.FullUri && value < 0x80 && UriReserved.Contains((char)value, StringComparison.Ordinal))
                {
                    if (FlushBytes(pending, pendingStart, output) is { } flushError)
                    {
                        return flushError;
                    }

                    output.Append(text, i, 3);
                }
                else
                {
                    if (pending.Count == 0)
                    {
                        pendingStart = i;
                    }

                    pending.Add(value);
                }

                i += 2;
                continue;
            }

            if (FlushBytes(pending, pendingStart, output) is { } error)
            {
                return error;
            }

            output.Append(c == '+' && plusAsSpace ? ' ' : c);
        }

        return FlushBytes(pending, pendingStart, output) is { } finalError ? finalError : output.ToString();
    }

    private static bool IsUnescaped(char c, UrlEncodeMode mode) =>
        char.IsAsciiLetterOrDigit(c)
        || ComponentSafe.Contains(c, StringComparison.Ordinal)
        || (mode == UrlEncodeMode.FullUri && UriReserved.Contains(c, StringComparison.Ordinal));

    private static Error? FlushBytes(List<byte> pending, int start, StringBuilder output)
    {
        if (pending.Count == 0)
        {
            return null;
        }

        if (!StrictUtf8.TryDecode(pending.ToArray(), out var decoded))
        {
            return new Error("url.invalid-utf8", CoreLocalization.Format(CoreStrings.Url_InvalidUtf8, start + 1))
            {
                Location = new ErrorLocation(Offset: start),
            };
        }

        output.Append(decoded);
        pending.Clear();
        return null;
    }
}

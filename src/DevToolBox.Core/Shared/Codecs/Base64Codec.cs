using System.Buffers.Text;
using System.Text;

namespace DevToolBox.Core.Shared.Codecs;

public static class Base64Codec
{
    public static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes);

    /// <summary>Base64URL (RFC 4648 §5) without padding, as used by JWT.</summary>
    public static string EncodeUrl(ReadOnlySpan<byte> bytes) => Base64Url.EncodeToString(bytes);

    /// <summary>
    /// Lenient decode: accepts the standard and URL-safe alphabets, optional padding and embedded
    /// whitespace (line-wrapped Base64).
    /// </summary>
    public static bool TryDecode(string? input, out byte[] bytes)
    {
        bytes = [];
        if (input is null || Normalize(input) is not { } normalized)
        {
            return false;
        }

        var buffer = new byte[normalized.Length / 4 * 3];
        if (!Convert.TryFromBase64String(normalized, buffer, out var written))
        {
            return false;
        }

        bytes = buffer.AsSpan(0, written).ToArray();
        return true;
    }

    /// <summary>True when the text uses only Base64/Base64URL characters with valid padding.</summary>
    public static bool LooksLikeBase64(string input) => Normalize(input) is { Length: > 0 };

    private static string? Normalize(string input)
    {
        var builder = new StringBuilder(input.Length + 3);
        var padding = 0;
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c == '=')
            {
                padding++;
                continue;
            }

            // Data after padding is invalid.
            if (padding > 0)
            {
                return null;
            }

            switch (c)
            {
                case >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/':
                    builder.Append(c);
                    break;
                case '-':
                    builder.Append('+');
                    break;
                case '_':
                    builder.Append('/');
                    break;
                default:
                    return null;
            }
        }

        if (padding > 2 || builder.Length % 4 == 1)
        {
            return null;
        }

        while (builder.Length % 4 != 0)
        {
            builder.Append('=');
        }

        return builder.ToString();
    }
}

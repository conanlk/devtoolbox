using System.Text;

namespace DevToolBox.Core.Shared.Codecs;

/// <summary>UTF-8 decoding that reports invalid bytes instead of silently inserting U+FFFD.</summary>
public static class StrictUtf8
{
    private static readonly UTF8Encoding Decoder = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryDecode(ReadOnlySpan<byte> bytes, out string text)
    {
        try
        {
            text = Decoder.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    /// <summary>True when the text contains no control characters other than tab, CR and LF.</summary>
    public static bool IsPrintable(string text)
    {
        foreach (var c in text)
        {
            if (char.IsControl(c) && c is not ('\t' or '\r' or '\n'))
            {
                return false;
            }
        }

        return true;
    }
}

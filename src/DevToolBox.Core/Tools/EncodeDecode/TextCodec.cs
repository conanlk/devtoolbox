using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Codecs;

namespace DevToolBox.Core.Tools.EncodeDecode;

public enum TextEncoding
{
    Base64,
    Base64Url,
    Hex,
    Binary,
}

public enum CodecDirection
{
    Encode,
    Decode,
}

/// <summary>Converts UTF-8 text to and from Base64, Base64URL, hex and binary.</summary>
public static class TextCodec
{
    public static string Encode(string? text, TextEncoding encoding)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        return encoding switch
        {
            TextEncoding.Base64 => Base64Codec.Encode(bytes),
            TextEncoding.Base64Url => Base64Codec.EncodeUrl(bytes),
            TextEncoding.Hex => Convert.ToHexStringLower(bytes),
            TextEncoding.Binary => ToBinary(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    public static Result<string> Decode(string? input, TextEncoding encoding)
    {
        var bytes = DecodeBytes(input, encoding);
        if (!bytes.IsOk)
        {
            return bytes.Error;
        }

        return StrictUtf8.TryDecode(bytes.Value, out var text)
            ? text
            : new Error("codec.not-utf8", CoreStrings.Codec_NotUtf8);
    }

    public static Result<byte[]> DecodeBytes(string? input, TextEncoding encoding)
    {
        input ??= string.Empty;
        return encoding switch
        {
            TextEncoding.Base64 or TextEncoding.Base64Url => Base64Codec.TryDecode(input, out var bytes)
                ? bytes
                : new Error("codec.invalid-base64", CoreStrings.Codec_InvalidBase64),
            TextEncoding.Hex => DecodeHex(input),
            TextEncoding.Binary => DecodeBinary(input),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    /// <summary>
    /// Guesses the direction: Decode when the input is valid in the chosen encoding and decodes to
    /// printable UTF-8 text; otherwise Encode.
    /// </summary>
    public static CodecDirection DetectDirection(string? input, TextEncoding encoding)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CodecDirection.Encode;
        }

        if (encoding is TextEncoding.Base64 or TextEncoding.Base64Url && input.Trim().Length < 4)
        {
            return CodecDirection.Encode;
        }

        var decoded = Decode(input, encoding);
        return decoded.IsOk && decoded.Value.Length > 0 && StrictUtf8.IsPrintable(decoded.Value)
            ? CodecDirection.Decode
            : CodecDirection.Encode;
    }

    private static string ToBinary(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 9);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Convert.ToString(bytes[i], 2).PadLeft(8, '0'));
        }

        return builder.ToString();
    }

    private static Result<byte[]> DecodeHex(string input)
    {
        // Accept "48 65", "48:65", "0x48 0x65" and "48-65".
        var builder = new StringBuilder(input.Length);
        foreach (var token in input.Split([' ', '\t', '\r', '\n', ':', '-', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append(token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token);
        }

        var hex = builder.ToString();
        if (hex.Length % 2 != 0)
        {
            return new Error("codec.invalid-hex", CoreStrings.Codec_HexOdd);
        }

        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return new Error("codec.invalid-hex", CoreLocalization.Format(CoreStrings.Codec_NotHexDigit, c));
            }
        }

        return Convert.FromHexString(hex);
    }

    private static Result<byte[]> DecodeBinary(string input)
    {
        var bits = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c is '0' or '1')
            {
                bits.Append(c);
            }
            else if (!char.IsWhiteSpace(c))
            {
                return new Error("codec.invalid-binary", CoreLocalization.Format(CoreStrings.Codec_NotBinaryDigit, c));
            }
        }

        if (bits.Length % 8 != 0)
        {
            return new Error("codec.invalid-binary", CoreStrings.Codec_BinaryWholeBytes);
        }

        var bytes = new byte[bits.Length / 8];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(bits.ToString(i * 8, 8), 2);
        }

        return bytes;
    }
}

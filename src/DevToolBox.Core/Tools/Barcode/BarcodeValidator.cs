using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Barcode;

/// <summary>Per-symbology input rules. Returns the exact text to encode.</summary>
public static class BarcodeValidator
{
    public const int MaxQrBytes = 2953;
    public const int MaxLinearLength = 80;
    private const string Code39Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";

    public static Result<string> Normalize(BarcodeKind kind, string? text)
    {
        var value = kind == BarcodeKind.QrCode ? text ?? string.Empty : (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new Error("barcode.empty", CoreStrings.Barcode_Empty);
        }

        return kind switch
        {
            BarcodeKind.QrCode => Encoding.UTF8.GetByteCount(value) <= MaxQrBytes
                ? value
                : new Error("barcode.too-long", CoreLocalization.Format(CoreStrings.Barcode_QrTooLong, MaxQrBytes)),
            BarcodeKind.Ean13 => Gtin(value, 13, "EAN-13"),
            BarcodeKind.UpcA => Gtin(value, 12, "UPC-A"),
            BarcodeKind.Code39 => Code39(value),
            BarcodeKind.Code128 => Code128(value),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>GS1 mod-10 check digit for the given digits (without the check digit).</summary>
    public static int CheckDigit(string digitsWithoutCheck)
    {
        ArgumentNullException.ThrowIfNull(digitsWithoutCheck);
        var sum = 0;
        for (var i = 0; i < digitsWithoutCheck.Length; i++)
        {
            var digit = digitsWithoutCheck[digitsWithoutCheck.Length - 1 - i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }

        return (10 - (sum % 10)) % 10;
    }

    private static Result<string> Gtin(string value, int length, string name)
    {
        if (!value.All(char.IsAsciiDigit))
        {
            return new Error("barcode.digits", CoreLocalization.Format(CoreStrings.Barcode_DigitsOnly, name));
        }

        if (value.Length == length - 1)
        {
            return value + CheckDigit(value);
        }

        if (value.Length != length)
        {
            return new Error("barcode.length", CoreLocalization.Format(CoreStrings.Barcode_Length, name, length - 1, length));
        }

        var expected = CheckDigit(value[..^1]);
        return value[^1] - '0' == expected
            ? value
            : new Error("barcode.checksum", CoreLocalization.Format(CoreStrings.Barcode_Checksum, expected));
    }

    private static Result<string> Code39(string value)
    {
        var upper = value.ToUpperInvariant();
        var invalid = upper.FirstOrDefault(c => !Code39Characters.Contains(c, StringComparison.Ordinal));
        if (invalid != default)
        {
            return new Error("barcode.charset", CoreLocalization.Format(CoreStrings.Barcode_Code39Charset, invalid));
        }

        return upper.Length <= MaxLinearLength ? upper : TooLong("Code 39");
    }

    private static Result<string> Code128(string value)
    {
        var invalid = value.FirstOrDefault(c => c is < ' ' or > '~');
        if (invalid != default)
        {
            return new Error("barcode.charset", CoreLocalization.Format(CoreStrings.Barcode_Code128Charset, invalid));
        }

        return value.Length <= MaxLinearLength ? value : TooLong("Code 128");
    }

    private static Error TooLong(string name) =>
        new("barcode.too-long", CoreLocalization.Format(CoreStrings.Barcode_TooLong, name, MaxLinearLength));
}

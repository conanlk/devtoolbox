using System.Text.RegularExpressions;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared.Codecs;
using DevToolBox.Core.Shared.Json;
using DevToolBox.Core.Tools.Jwt;

namespace DevToolBox.Core.Tools.SmartPaste;

/// <summary>What to do with pasted content: which tool, and how it should treat the input.</summary>
/// <param name="ToolId">Registry id of the suggested tool.</param>
/// <param name="Label">Button text in the current UI language, e.g. "Open in JWT decoder".</param>
/// <param name="Intent">Optional hint for the tool (e.g. "decode", "parse", "compare").</param>
public sealed record ContentSuggestion(string ToolId, string Label, string? Intent = null);

/// <param name="Kind">Stable English name of what was recognized, e.g. "JWT" or "Unix timestamp".</param>
/// <param name="KindLabel">The same in the current UI language, for display.</param>
public sealed record ContentDetection(string Kind, string KindLabel, IReadOnlyList<ContentSuggestion> Suggestions);

/// <summary>
/// Recognizes common developer payloads (JWT, JSON, UUID, epoch, URL, Base64, hashes…) in text the user
/// pasted, and suggests tools — best match first. It only ever looks at text it is given.
/// </summary>
public static partial class ContentDetector
{
    /// <summary>Larger input is only checked by cheap rules (no JSON parse, no Base64 decode).</summary>
    public const int MaxDeepInspectionChars = 1_000_000;

    public static ContentDetection Detect(string? input, DateTimeOffset now)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new ContentDetection("empty", string.Empty, []);
        }

        var deep = text.Length <= MaxDeepInspectionChars;
        var (kind, label, suggestions) = Classify(text, deep, now);
        return new ContentDetection(kind, label, [.. suggestions.DistinctBy(s => (s.ToolId, s.Intent)).Take(3)]);
    }

    private static (string Kind, string KindLabel, List<ContentSuggestion> Suggestions) Classify(string text, bool deep, DateTimeOffset now)
    {
        if (deep && LooksLikeJwt(text, now))
        {
            return ("JWT", "JWT", [Jwt(), Base64Decode(), HashIt()]);
        }

        if (text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return ("Image data URI", CoreStrings.SmartPaste_Kind_ImageDataUri,
                [new("image-base64", CoreStrings.SmartPaste_PreviewImage, "decode"), new("app-icons", CoreStrings.SmartPaste_MakeAppIcons)]);
        }

        if (deep && text[0] is '{' or '[' && JsonTreeParser.Parse(text).IsOk)
        {
            return ("JSON", "JSON", [new("json-formatter", CoreStrings.SmartPaste_OpenJsonFormatter), HashIt()]);
        }

        if (text.Length <= 45 && Guid.TryParse(text.Replace("urn:uuid:", string.Empty, StringComparison.OrdinalIgnoreCase), out _))
        {
            return ("UUID", "UUID", [new("uuid-generator", CoreStrings.SmartPaste_InspectUuid, "inspect")]);
        }

        if (EpochNumber().IsMatch(text))
        {
            return ("Unix timestamp", CoreStrings.SmartPaste_Kind_UnixTimestamp, [new("timestamp", CoreStrings.SmartPaste_ConvertTimestamp)]);
        }

        if (HashLike().IsMatch(text) && text.Length is 8 or 32 or 40 or 64 or 96 or 128)
        {
            return ("Hash", CoreStrings.SmartPaste_Kind_Hash,
                [new("hash-generator", CoreStrings.SmartPaste_CompareHash, "compare"), new("encode-decode", CoreStrings.SmartPaste_HexDecode, "decode")]);
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "ftp" && !text.Contains(' ', StringComparison.Ordinal))
        {
            return ("URL", "URL",
            [
                new("url-encode", CoreStrings.SmartPaste_ParseUrl, "parse"),
                new("url-encode", CoreStrings.SmartPaste_UrlEncode, "encode"),
                new("barcode-generator", CoreStrings.SmartPaste_MakeQrCode),
            ]);
        }

        if (PercentEscape().IsMatch(text))
        {
            return ("URL-encoded text", CoreStrings.SmartPaste_Kind_UrlEncodedText, [new("url-encode", CoreStrings.SmartPaste_UrlDecode, "decode")]);
        }

        if (HtmlEntity().IsMatch(text))
        {
            return ("HTML entities", CoreStrings.SmartPaste_Kind_HtmlEntities, [new("html-encode", CoreStrings.SmartPaste_HtmlDecode, "decode")]);
        }

        if (deep && LooksLikeBase64Text(text))
        {
            return ("Base64", "Base64", [Base64Decode(), HashIt()]);
        }

        var suggestions = new List<ContentSuggestion>();
        if (text.Contains('<', StringComparison.Ordinal) && text.Contains('>', StringComparison.Ordinal))
        {
            suggestions.Add(new("html-encode", CoreStrings.SmartPaste_HtmlEncode, "encode"));
        }

        suggestions.Add(HashIt());
        suggestions.Add(new("encode-decode", CoreStrings.SmartPaste_Base64Encode, "encode"));
        suggestions.Add(new("regex-tester", CoreStrings.SmartPaste_TestRegex));
        return ("Text", CoreStrings.SmartPaste_Kind_Text, suggestions);
    }

    private static ContentSuggestion Jwt() => new("jwt-decoder", CoreStrings.SmartPaste_OpenJwtDecoder);

    private static ContentSuggestion Base64Decode() => new("encode-decode", CoreStrings.SmartPaste_Base64Decode, "decode");

    private static ContentSuggestion HashIt() => new("hash-generator", CoreStrings.SmartPaste_HashIt);

    private static bool LooksLikeJwt(string text, DateTimeOffset now) =>
        JwtShape().IsMatch(text) && JwtDecoder.Decode(text, now).IsOk;

    private static bool LooksLikeBase64Text(string text)
    {
        if (text.Length < 8 || !Base64Codec.LooksLikeBase64(text) || !Base64Codec.TryDecode(text, out var bytes))
        {
            return false;
        }

        // Plain words like "password" are valid Base64 too; require the decoded bytes to be readable text.
        return StrictUtf8.TryDecode(bytes, out var decoded) && decoded.Length > 0 && StrictUtf8.IsPrintable(decoded);
    }

    [GeneratedRegex(@"^(Bearer\s+)?[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]*$")]
    private static partial Regex JwtShape();

    [GeneratedRegex(@"^-?\d{9,19}(\.\d+)?$")]
    private static partial Regex EpochNumber();

    [GeneratedRegex("^[0-9a-fA-F]+$")]
    private static partial Regex HashLike();

    [GeneratedRegex("%[0-9A-Fa-f]{2}")]
    private static partial Regex PercentEscape();

    [GeneratedRegex(@"&(#\d+|#x[0-9A-Fa-f]+|[A-Za-z]+);")]
    private static partial Regex HtmlEntity();
}

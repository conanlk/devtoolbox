using System.Text.Json;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Codecs;
using DevToolBox.Core.Shared.Json;

namespace DevToolBox.Core.Tools.Jwt;

public enum JwtStatus
{
    Valid,
    Expired,
    NotYetValid,
    NoExpiry,
}

public sealed record JwtTimeClaim(string Name, string Description, string RawValue, DateTimeOffset? Value);

/// <summary>A registered claim (RFC 7519 §4.1) present in the payload.</summary>
public sealed record JwtClaim(string Name, string Description, string RawValue, DateTimeOffset? Time);

public sealed record JwtToken(
    string HeaderJson,
    string PayloadJson,
    string? Algorithm,
    string? Type,
    IReadOnlyList<JwtTimeClaim> TimeClaims,
    JwtStatus Status,
    string Signature,
    string SigningInput)
{
    /// <summary>The three encoded parts, for color-coded display.</summary>
    public (string Header, string Payload, string Signature) Parts
    {
        get
        {
            var dot = SigningInput.IndexOf('.', StringComparison.Ordinal);
            return (SigningInput[..dot], SigningInput[(dot + 1)..], Signature);
        }
    }

    public IReadOnlyList<JwtClaim> RegisteredClaims { get; init; } = [];

    public bool IsUnsecured => string.Equals(Algorithm, "none", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Decodes (does not verify) a JWS compact-serialized JWT.</summary>
public static class JwtDecoder
{
    private static readonly string[] RegisteredClaimNames = ["iss", "sub", "aud", "exp", "nbf", "iat", "jti"];

    private static readonly string[] KnownTimeClaims = ["iat", "nbf", "exp", "auth_time"];

    /// <summary>Human name of a registered or time claim, in the current UI language.</summary>
    public static string ClaimDescription(string name) => name switch
    {
        "iss" => CoreStrings.Jwt_Claim_Issuer,
        "sub" => CoreStrings.Jwt_Claim_Subject,
        "aud" => CoreStrings.Jwt_Claim_Audience,
        "exp" => CoreStrings.Jwt_Claim_Expires,
        "nbf" => CoreStrings.Jwt_Claim_NotBefore,
        "iat" => CoreStrings.Jwt_Claim_IssuedAt,
        "jti" => CoreStrings.Jwt_Claim_JwtId,
        "auth_time" => CoreStrings.Jwt_Claim_AuthenticatedAt,
        _ => name,
    };

    public static Result<JwtToken> Decode(string? token, DateTimeOffset now)
    {
        var compact = Normalize(token);
        if (compact.Length == 0)
        {
            return new Error("jwt.empty", CoreStrings.Jwt_Empty);
        }

        var parts = compact.Split('.');
        if (parts.Length == 5)
        {
            return new Error("jwt.encrypted", CoreStrings.Jwt_Encrypted);
        }

        if (parts.Length != 3)
        {
            return new Error("jwt.parts", CoreLocalization.Format(CoreStrings.Jwt_Parts, parts.Length));
        }

        var header = DecodeJsonObject(parts[0], CoreStrings.Jwt_Header);
        if (!header.IsOk)
        {
            return header.Error;
        }

        var payload = DecodeJsonObject(parts[1], CoreStrings.Jwt_Payload);
        if (!payload.IsOk)
        {
            return payload.Error;
        }

        using var headerDoc = JsonDocument.Parse(header.Value.Text);
        using var payloadDoc = JsonDocument.Parse(payload.Value.Text);
        var claims = ReadTimeClaims(payloadDoc.RootElement);

        return new JwtToken(
            JsonTreeWriter.Write(header.Value.Tree, JsonIndent.TwoSpaces),
            JsonTreeWriter.Write(payload.Value.Tree, JsonIndent.TwoSpaces),
            ReadString(headerDoc.RootElement, "alg"),
            ReadString(headerDoc.RootElement, "typ"),
            claims,
            Evaluate(claims, now),
            parts[2],
            parts[0] + "." + parts[1])
        {
            RegisteredClaims = ReadRegisteredClaims(payloadDoc.RootElement, claims),
        };
    }

    /// <summary>Removes whitespace and an optional "Bearer " prefix.</summary>
    public static string Normalize(string? token)
    {
        var text = (token ?? string.Empty).Trim();
        if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..];
        }

        return string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    }

    private static Result<(string Text, JsonTreeNode Tree)> DecodeJsonObject(string segment, string name)
    {
        if (!Base64Codec.TryDecode(segment, out var bytes))
        {
            return new Error("jwt.base64", CoreLocalization.Format(CoreStrings.Jwt_PartNotBase64Url, name));
        }

        if (!StrictUtf8.TryDecode(bytes, out var json))
        {
            return new Error("jwt.utf8", CoreLocalization.Format(CoreStrings.Jwt_PartNotUtf8, name));
        }

        var tree = JsonTreeParser.Parse(json);
        if (!tree.IsOk || tree.Value.Root.Kind != JsonNodeKind.Object)
        {
            return new Error("jwt.json", CoreLocalization.Format(CoreStrings.Jwt_PartNotJsonObject, name));
        }

        return (json, tree.Value.Root);
    }

    private static List<JwtTimeClaim> ReadTimeClaims(JsonElement payload)
    {
        var claims = new List<JwtTimeClaim>();
        foreach (var name in KnownTimeClaims)
        {
            if (!payload.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            DateTimeOffset? value = null;
            if (element.TryGetDouble(out var seconds)
                && seconds is >= -62_135_596_800 and <= 253_402_300_799)
            {
                value = DateTimeOffset.UnixEpoch.AddTicks((long)(seconds * TimeSpan.TicksPerSecond));
            }

            claims.Add(new JwtTimeClaim(name, ClaimDescription(name), element.GetRawText(), value));
        }

        return claims;
    }

    private static List<JwtClaim> ReadRegisteredClaims(JsonElement payload, List<JwtTimeClaim> timeClaims)
    {
        var claims = new List<JwtClaim>();
        foreach (var name in RegisteredClaimNames)
        {
            if (payload.TryGetProperty(name, out var element))
            {
                var raw = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
                claims.Add(new JwtClaim(name, ClaimDescription(name), raw, timeClaims.FirstOrDefault(c => c.Name == name)?.Value));
            }
        }

        return claims;
    }

    private static JwtStatus Evaluate(IReadOnlyList<JwtTimeClaim> claims, DateTimeOffset now)
    {
        var exp = claims.FirstOrDefault(c => c.Name == "exp")?.Value;
        var nbf = claims.FirstOrDefault(c => c.Name == "nbf")?.Value;
        if (exp is { } expires && now >= expires)
        {
            return JwtStatus.Expired;
        }

        if (nbf is { } notBefore && now < notBefore)
        {
            return JwtStatus.NotYetValid;
        }

        return exp is null ? JwtStatus.NoExpiry : JwtStatus.Valid;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

}

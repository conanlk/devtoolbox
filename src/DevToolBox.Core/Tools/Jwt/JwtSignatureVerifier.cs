using System.Security.Cryptography;
using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Codecs;

namespace DevToolBox.Core.Tools.Jwt;

/// <summary>Verifies HMAC (HS256/HS384/HS512) signatures with a user-supplied shared secret.</summary>
public static class JwtSignatureVerifier
{
    public static Result<bool> VerifyHmac(JwtToken token, string? secret, bool secretIsBase64 = false)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrEmpty(secret))
        {
            return new Error("jwt.secret-empty", CoreStrings.Jwt_SecretEmpty);
        }

        Func<byte[], byte[], byte[]>? hmac = token.Algorithm?.ToUpperInvariant() switch
        {
            "HS256" => HMACSHA256.HashData,
            "HS384" => HMACSHA384.HashData,
            "HS512" => HMACSHA512.HashData,
            _ => null,
        };
        if (hmac is null)
        {
            return new Error("jwt.alg-unsupported",
                CoreLocalization.Format(CoreStrings.Jwt_AlgUnsupported, token.Algorithm ?? CoreStrings.Jwt_NoAlgorithm));
        }

        byte[] key;
        if (secretIsBase64)
        {
            if (!Base64Codec.TryDecode(secret, out key))
            {
                return new Error("jwt.secret-base64", CoreStrings.Jwt_SecretNotBase64);
            }
        }
        else
        {
            key = Encoding.UTF8.GetBytes(secret);
        }

        if (!Base64Codec.TryDecode(token.Signature, out var signature))
        {
            return new Error("jwt.signature-base64", CoreStrings.Jwt_SignatureNotBase64Url);
        }

        var expected = hmac(key, Encoding.ASCII.GetBytes(token.SigningInput));
        return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}

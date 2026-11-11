using DevToolBox.Core.Tools.Jwt;

namespace DevToolBox.Core.Tests.Tools.Jwt;

public class JwtDecoderTests
{
    // jwt.io sample: HS256, secret "your-256-bit-secret", iat 1516239022, no exp.
    private const string Sample =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decode_PrettyPrintsHeaderAndPayload()
    {
        var token = JwtDecoder.Decode(Sample, Now).Value;

        Assert.Equal("{\n  \"alg\": \"HS256\",\n  \"typ\": \"JWT\"\n}", token.HeaderJson);
        Assert.Contains("\"name\": \"John Doe\"", token.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("HS256", token.Algorithm);
        Assert.Equal("JWT", token.Type);
        Assert.Equal(JwtStatus.NoExpiry, token.Status);
        var iat = Assert.Single(token.TimeClaims);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1516239022), iat.Value);
    }

    [Fact]
    public void Decode_ExposesPartsAndRegisteredClaims()
    {
        var token = JwtDecoder.Decode(Sample, Now).Value;

        Assert.Equal("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", token.Parts.Header);
        Assert.StartsWith("eyJzdWIi", token.Parts.Payload, StringComparison.Ordinal);
        Assert.Equal("SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", token.Parts.Signature);
        Assert.Equal(["sub", "iat"], token.RegisteredClaims.Select(c => c.Name));
        Assert.Equal("1234567890", token.RegisteredClaims[0].RawValue);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1516239022), token.RegisteredClaims[1].Time);
    }

    [Theory]
    [InlineData("Bearer " + Sample)]
    [InlineData("  " + Sample + "\n")]
    public void Decode_AcceptsBearerPrefixAndWhitespace(string input) =>
        Assert.True(JwtDecoder.Decode(input, Now).IsOk);

    [Theory]
    [InlineData(-60, JwtStatus.Expired)]
    [InlineData(3600, JwtStatus.Valid)]
    public void Decode_EvaluatesExpiry(int expOffsetSeconds, JwtStatus expected)
    {
        var exp = Now.AddSeconds(expOffsetSeconds).ToUnixTimeSeconds();
        var token = JwtTestTokens.Build($"{{\"alg\":\"none\"}}", $"{{\"exp\":{exp}}}");

        Assert.Equal(expected, JwtDecoder.Decode(token, Now).Value.Status);
    }

    [Fact]
    public void Decode_NotBeforeInFuture_IsNotYetValid()
    {
        var token = JwtTestTokens.Build("{\"alg\":\"none\"}", $"{{\"nbf\":{Now.AddDays(1).ToUnixTimeSeconds()}}}");

        var decoded = JwtDecoder.Decode(token, Now).Value;
        Assert.Equal(JwtStatus.NotYetValid, decoded.Status);
        Assert.True(decoded.IsUnsecured);
    }

    [Theory]
    [InlineData("", "jwt.empty")]
    [InlineData("abc.def", "jwt.parts")]
    [InlineData("a.b.c.d.e", "jwt.encrypted")]
    [InlineData("!!!.eyJhIjoxfQ.sig", "jwt.base64")]
    [InlineData("eyJhIjox.eyJhIjoxfQ.sig", "jwt.json")]
    [InlineData("WzFd.eyJhIjoxfQ.sig", "jwt.json")]
    public void Decode_Malformed_GivesClearError(string token, string code) =>
        Assert.Equal(code, JwtDecoder.Decode(token, Now).Error?.Code);
}

public class JwtSignatureVerifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static JwtToken Sample() => JwtDecoder.Decode(
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", Now).Value;

    [Fact]
    public void CorrectSecret_Verifies() => Assert.True(JwtSignatureVerifier.VerifyHmac(Sample(), "your-256-bit-secret").Value);

    [Fact]
    public void WrongSecret_DoesNotVerify() => Assert.False(JwtSignatureVerifier.VerifyHmac(Sample(), "wrong").Value);

    [Fact]
    public void Base64Secret_IsDecodedFirst() =>
        Assert.True(JwtSignatureVerifier.VerifyHmac(Sample(), Convert.ToBase64String("your-256-bit-secret"u8), secretIsBase64: true).Value);

    [Theory]
    [InlineData("HS384")]
    [InlineData("HS512")]
    public void OtherHmacSizes_Verify(string alg)
    {
        var token = JwtTestTokens.BuildSigned(alg, "{\"sub\":\"1\"}", "secret");

        Assert.True(JwtSignatureVerifier.VerifyHmac(JwtDecoder.Decode(token, Now).Value, "secret").Value);
    }

    [Fact]
    public void AsymmetricAlgorithm_IsReportedAsUnsupported()
    {
        var token = JwtDecoder.Decode(JwtTestTokens.Build("{\"alg\":\"RS256\"}", "{}"), Now).Value;

        Assert.Equal("jwt.alg-unsupported", JwtSignatureVerifier.VerifyHmac(token, "x").Error?.Code);
    }

    [Fact]
    public void EmptySecret_IsAnError() => Assert.Equal("jwt.secret-empty", JwtSignatureVerifier.VerifyHmac(Sample(), "").Error?.Code);
}

internal static class JwtTestTokens
{
    public static string Build(string header, string payload, string signature = "") =>
        $"{Encode(header)}.{Encode(payload)}.{signature}";

    public static string BuildSigned(string alg, string payload, string secret)
    {
        var signingInput = $"{Encode($"{{\"alg\":\"{alg}\"}}")}.{Encode(payload)}";
        var key = System.Text.Encoding.UTF8.GetBytes(secret);
        var data = System.Text.Encoding.ASCII.GetBytes(signingInput);
        var signature = alg switch
        {
            "HS384" => System.Security.Cryptography.HMACSHA384.HashData(key, data),
            _ => System.Security.Cryptography.HMACSHA512.HashData(key, data),
        };
        return $"{signingInput}.{System.Buffers.Text.Base64Url.EncodeToString(signature)}";
    }

    private static string Encode(string json) => System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(json));
}

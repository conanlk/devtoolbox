using System.Text;
using DevToolBox.Core.Tools.Hash;

namespace DevToolBox.Core.Tests.Tools.Hash;

public class HashCalculatorTests
{
    private static readonly HashAlgorithmKind[] Classic =
        [HashAlgorithmKind.Md5, HashAlgorithmKind.Sha1, HashAlgorithmKind.Sha256, HashAlgorithmKind.Sha384, HashAlgorithmKind.Sha512, HashAlgorithmKind.Crc32];

    [Fact]
    public void ComputeText_MatchesKnownVectors()
    {
        var hashes = HashCalculator.ComputeText("abc", Classic).Value;

        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Hex(hashes[HashAlgorithmKind.Md5]));
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", Hex(hashes[HashAlgorithmKind.Sha1]));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hex(hashes[HashAlgorithmKind.Sha256]));
        Assert.StartsWith("cb00753f45a35e8b", Hex(hashes[HashAlgorithmKind.Sha384]), StringComparison.Ordinal);
        Assert.StartsWith("ddaf35a193617aba", Hex(hashes[HashAlgorithmKind.Sha512]), StringComparison.Ordinal);
        Assert.Equal("352441c2", Hex(hashes[HashAlgorithmKind.Crc32]));
    }

    [Fact]
    public void ComputeText_EmptyAndUnicode()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Hex(HashCalculator.ComputeText("", [HashAlgorithmKind.Sha256]).Value[HashAlgorithmKind.Sha256]));
        Assert.Equal(Hex(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("Xin chào 👋"))),
            Hex(HashCalculator.ComputeText("Xin chào 👋", [HashAlgorithmKind.Sha256]).Value[HashAlgorithmKind.Sha256]));
    }

    [Fact]
    public void Hmac_MatchesRfc4231TestCase2()
    {
        var hashes = HashCalculator.ComputeText("what do ya want for nothing?", Classic, hmacKey: "Jefe").Value;

        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", Hex(hashes[HashAlgorithmKind.Sha256]));
        Assert.False(hashes.ContainsKey(HashAlgorithmKind.Crc32));
    }

    [Fact]
    public void UnsupportedSha3_IsSkippedNotThrown()
    {
        var hashes = HashCalculator.ComputeText("abc", [HashAlgorithmKind.Sha3_256]).Value;

        Assert.Equal(HashAlgorithms.IsSupported(HashAlgorithmKind.Sha3_256), hashes.ContainsKey(HashAlgorithmKind.Sha3_256));
        if (hashes.TryGetValue(HashAlgorithmKind.Sha3_256, out var sha3))
        {
            Assert.Equal("3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532", Hex(sha3));
        }
    }

    [Fact]
    public async Task ComputeStream_MatchesTextHash_AndReportsProgress()
    {
        var data = new byte[3 * 1024 * 1024 + 123];
        System.Security.Cryptography.RandomNumberGenerator.Fill(data);
        var progress = new List<long>();

        var result = await HashCalculator.ComputeStreamAsync(
            new MemoryStream(data), [HashAlgorithmKind.Sha256, HashAlgorithmKind.Md5], progress: new SyncProgress(progress.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(System.Security.Cryptography.SHA256.HashData(data), result.Value[HashAlgorithmKind.Sha256]);
        Assert.Equal(data.Length, progress[^1]);
        Assert.Equal(4, progress.Count);
    }

    [Fact]
    public async Task ComputeStream_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HashCalculator.ComputeStreamAsync(new MemoryStream(new byte[10]), [HashAlgorithmKind.Sha256], cancellationToken: cts.Token));
    }

    [Theory]
    [InlineData(HashOutputFormat.HexLower, "deadbeef")]
    [InlineData(HashOutputFormat.HexUpper, "DEADBEEF")]
    [InlineData(HashOutputFormat.Base64, "3q2+7w==")]
    public void Format_Outputs(HashOutputFormat format, string expected) =>
        Assert.Equal(expected, HashCalculator.Format([0xDE, 0xAD, 0xBE, 0xEF], format));

    [Theory]
    [InlineData("DEADBEEF", true)]
    [InlineData(" de:ad:be:ef ", true)]
    [InlineData("de-ad-be-ef", true)]
    [InlineData("3q2+7w==", true)]
    [InlineData("deadbeee", false)]
    [InlineData("", false)]
    public void Matches_ComparesHexOrBase64(string expected, bool matches) =>
        Assert.Equal(matches, HashCalculator.Matches([0xDE, 0xAD, 0xBE, 0xEF], expected));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);

    private sealed class SyncProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}

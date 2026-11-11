using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Hash;

public static class HashCalculator
{
    private const int BufferSize = 1024 * 1024;

    public static Result<IReadOnlyDictionary<HashAlgorithmKind, byte[]>> ComputeText(
        string? text, IEnumerable<HashAlgorithmKind> algorithms, string? hmacKey = null)
    {
        try
        {
            using var hasher = new MultiHasher(Supported(algorithms, hmacKey), KeyBytes(hmacKey));
            hasher.Append(Encoding.UTF8.GetBytes(text ?? string.Empty));
            return Result<IReadOnlyDictionary<HashAlgorithmKind, byte[]>>.Ok(hasher.Finish());
        }
        catch (CryptographicException ex)
        {
            return new Error("hash.crypto", CoreLocalization.Format(CoreStrings.Hash_Crypto, ex.Message));
        }
    }

    /// <summary>Streams the input in 1 MB chunks, reporting bytes processed.</summary>
    public static async Task<Result<IReadOnlyDictionary<HashAlgorithmKind, byte[]>>> ComputeStreamAsync(
        Stream stream,
        IEnumerable<HashAlgorithmKind> algorithms,
        string? hmacKey = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var hasher = new MultiHasher(Supported(algorithms, hmacKey), KeyBytes(hmacKey));
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.Append(buffer.AsSpan(0, read));
                total += read;
                progress?.Report(total);
            }

            return Result<IReadOnlyDictionary<HashAlgorithmKind, byte[]>>.Ok(hasher.Finish());
        }
        catch (CryptographicException ex)
        {
            return new Error("hash.crypto", CoreLocalization.Format(CoreStrings.Hash_Crypto, ex.Message));
        }
        catch (IOException ex)
        {
            return new Error("hash.io", CoreLocalization.Format(CoreStrings.Hash_Io, ex.Message));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static string Format(ReadOnlySpan<byte> hash, HashOutputFormat format) => format switch
    {
        HashOutputFormat.HexUpper => Convert.ToHexString(hash),
        HashOutputFormat.Base64 => Convert.ToBase64String(hash),
        _ => Convert.ToHexStringLower(hash),
    };

    /// <summary>
    /// True when <paramref name="expected"/> equals the hash as hex (any case, ignoring spaces, colons
    /// and dashes) or as Base64.
    /// </summary>
    public static bool Matches(ReadOnlySpan<byte> hash, string? expected)
    {
        var cleaned = string.Concat((expected ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c is not ':' and not '-'));
        if (cleaned.Length == 0)
        {
            return false;
        }

        if (cleaned.Equals(Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return cleaned == Convert.ToBase64String(hash);
    }

    private static IEnumerable<HashAlgorithmKind> Supported(IEnumerable<HashAlgorithmKind> algorithms, string? hmacKey) =>
        algorithms.Where(kind => hmacKey is null ? HashAlgorithms.IsSupported(kind) : HashAlgorithms.SupportsHmac(kind));

    private static byte[]? KeyBytes(string? hmacKey) => hmacKey is null ? null : Encoding.UTF8.GetBytes(hmacKey);
}

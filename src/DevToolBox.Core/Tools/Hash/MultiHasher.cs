using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace DevToolBox.Core.Tools.Hash;

/// <summary>Feeds the same bytes to several hash algorithms in one pass (one read of a large file).</summary>
public sealed class MultiHasher : IDisposable
{
    private readonly Dictionary<HashAlgorithmKind, IncrementalHash> _hashes = [];
    private readonly Crc32? _crc32;

    /// <param name="hmacKey">When set, every algorithm runs in HMAC mode (CRC32 is skipped).</param>
    public MultiHasher(IEnumerable<HashAlgorithmKind> algorithms, byte[]? hmacKey = null)
    {
        ArgumentNullException.ThrowIfNull(algorithms);
        foreach (var kind in algorithms.Distinct())
        {
            if (kind == HashAlgorithmKind.Crc32)
            {
                if (hmacKey is null)
                {
                    _crc32 = new Crc32();
                }

                continue;
            }

            var name = HashAlgorithms.ToName(kind);
            _hashes[kind] = hmacKey is null ? IncrementalHash.CreateHash(name) : IncrementalHash.CreateHMAC(name, hmacKey);
        }
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (var hash in _hashes.Values)
        {
            hash.AppendData(data);
        }

        _crc32?.Append(data);
    }

    public IReadOnlyDictionary<HashAlgorithmKind, byte[]> Finish()
    {
        var results = _hashes.ToDictionary(pair => pair.Key, pair => pair.Value.GetHashAndReset());
        if (_crc32 is not null)
        {
            // Conventional CRC32 notation is the big-endian form of the 32-bit value.
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, _crc32.GetCurrentHashAsUInt32());
            results[HashAlgorithmKind.Crc32] = bytes;
        }

        return results;
    }

    public void Dispose()
    {
        foreach (var hash in _hashes.Values)
        {
            hash.Dispose();
        }
    }
}

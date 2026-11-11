using System.Security.Cryptography;

namespace DevToolBox.Core.Tools.Hash;

public enum HashAlgorithmKind
{
    Md5,
    Sha1,
    Sha256,
    Sha384,
    Sha512,
    Sha3_256,
    Sha3_384,
    Sha3_512,
    Crc32,
}

public enum HashOutputFormat
{
    HexLower,
    HexUpper,
    Base64,
}

public static class HashAlgorithms
{
    public static IReadOnlyList<HashAlgorithmKind> All { get; } = Enum.GetValues<HashAlgorithmKind>();

    /// <summary>
    /// SHA-3 depends on the OS crypto provider (Windows 11 build 25324+, OpenSSL 1.1.1+ on Linux;
    /// not available on macOS).
    /// </summary>
    public static bool IsSupported(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Sha3_256 => SHA3_256.IsSupported,
        HashAlgorithmKind.Sha3_384 => SHA3_384.IsSupported,
        HashAlgorithmKind.Sha3_512 => SHA3_512.IsSupported,
        _ => true,
    };

    /// <summary>CRC32 is a checksum, not a keyed hash, so it has no HMAC mode.</summary>
    public static bool SupportsHmac(HashAlgorithmKind kind) => kind != HashAlgorithmKind.Crc32 && IsSupported(kind);

    public static string DisplayName(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Md5 => "MD5",
        HashAlgorithmKind.Sha1 => "SHA-1",
        HashAlgorithmKind.Sha256 => "SHA-256",
        HashAlgorithmKind.Sha384 => "SHA-384",
        HashAlgorithmKind.Sha512 => "SHA-512",
        HashAlgorithmKind.Sha3_256 => "SHA3-256",
        HashAlgorithmKind.Sha3_384 => "SHA3-384",
        HashAlgorithmKind.Sha3_512 => "SHA3-512",
        HashAlgorithmKind.Crc32 => "CRC32",
        _ => kind.ToString(),
    };

    internal static HashAlgorithmName ToName(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Md5 => HashAlgorithmName.MD5,
        HashAlgorithmKind.Sha1 => HashAlgorithmName.SHA1,
        HashAlgorithmKind.Sha256 => HashAlgorithmName.SHA256,
        HashAlgorithmKind.Sha384 => HashAlgorithmName.SHA384,
        HashAlgorithmKind.Sha512 => HashAlgorithmName.SHA512,
        HashAlgorithmKind.Sha3_256 => HashAlgorithmName.SHA3_256,
        HashAlgorithmKind.Sha3_384 => HashAlgorithmName.SHA3_384,
        HashAlgorithmKind.Sha3_512 => HashAlgorithmName.SHA3_512,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

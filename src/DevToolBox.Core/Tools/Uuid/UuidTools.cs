using System.Buffers.Binary;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Uuid;

public enum UuidVersion
{
    V4,
    V7,
}

public sealed record UuidFormat(bool Uppercase = false, bool Hyphens = true, bool Braces = false);

public sealed record UuidInfo(
    string Canonical,
    int Version,
    string VersionName,
    string Variant,
    DateTimeOffset? Timestamp,
    bool IsNil,
    bool IsMax);

public static class UuidTools
{
    public const int MaxCount = 10_000;

    private static readonly DateTimeOffset GregorianEpoch = new(1582, 10, 15, 0, 0, 0, TimeSpan.Zero);

    public static Result<IReadOnlyList<string>> Generate(UuidVersion version, int count, UuidFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var guids = GenerateGuids(version, count);
        return guids.IsOk ? Result<IReadOnlyList<string>>.Ok([.. guids.Value.Select(g => Format(g, format))]) : guids.Error;
    }

    /// <summary>v4: 122 random bits from the OS CSPRNG. v7: 48-bit Unix ms timestamp + random bits.</summary>
    public static Result<IReadOnlyList<Guid>> GenerateGuids(UuidVersion version, int count)
    {
        if (count is < 1 or > MaxCount)
        {
            return new Error("uuid.count", CoreLocalization.Format(CoreStrings.Uuid_Count, MaxCount));
        }

        var result = new Guid[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = version == UuidVersion.V7 ? Guid.CreateVersion7() : Guid.NewGuid();
        }

        return result;
    }

    public static string Format(Guid guid, UuidFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var text = guid.ToString(format.Hyphens ? "D" : "N");
        if (format.Braces)
        {
            text = "{" + text + "}";
        }

        return format.Uppercase ? text.ToUpperInvariant() : text;
    }

    /// <summary>Validates a UUID (any common notation) and reports version, variant and embedded time.</summary>
    public static Result<UuidInfo> Inspect(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
        {
            text = text[9..];
        }

        if (!Guid.TryParse(text, out var guid))
        {
            return new Error("uuid.invalid", CoreStrings.Uuid_Invalid);
        }

        var bytes = guid.ToByteArray(bigEndian: true);
        var isNil = bytes.All(b => b == 0);
        var isMax = bytes.All(b => b == 0xFF);
        var version = bytes[6] >> 4;

        return new UuidInfo(
            guid.ToString("D"),
            version,
            isNil ? "Nil UUID" : isMax ? "Max UUID" : VersionName(version),
            isNil || isMax ? "—" : VariantName(bytes[8]),
            isNil || isMax ? null : Timestamp(version, bytes),
            isNil,
            isMax);
    }

    private static string VersionName(int version) => version switch
    {
        1 => CoreStrings.Uuid_Version1,
        2 => CoreStrings.Uuid_Version2,
        3 => CoreStrings.Uuid_Version3,
        4 => CoreStrings.Uuid_Version4,
        5 => CoreStrings.Uuid_Version5,
        6 => CoreStrings.Uuid_Version6,
        7 => CoreStrings.Uuid_Version7,
        8 => CoreStrings.Uuid_Version8,
        _ => CoreLocalization.Format(CoreStrings.Uuid_VersionUnknown, version),
    };

    private static string VariantName(byte octet) =>
        (octet & 0x80) == 0 ? CoreStrings.Uuid_Variant_Ncs
        : (octet & 0xC0) == 0x80 ? "RFC 9562 / RFC 4122"
        : (octet & 0xE0) == 0xC0 ? CoreStrings.Uuid_Variant_Microsoft
        : CoreStrings.Uuid_Variant_Future;

    private static DateTimeOffset? Timestamp(int version, byte[] bytes)
    {
        try
        {
            switch (version)
            {
                case 7:
                    var unixMs = (long)(BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16);
                    return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
                case 1:
                    var low = BinaryPrimitives.ReadUInt32BigEndian(bytes);
                    var mid = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4));
                    var high = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6)) & 0x0FFF;
                    return GregorianEpoch.AddTicks((long)(((ulong)high << 48) | ((ulong)mid << 32) | low));
                case 6:
                    var top = BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16;
                    var rest = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6)) & 0x0FFF;
                    return GregorianEpoch.AddTicks((long)((top << 12) | (uint)rest));
                default:
                    return null;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

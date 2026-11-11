using DevToolBox.Core.Tools.Uuid;

namespace DevToolBox.Core.Tests.Tools.Uuid;

public class UuidToolsTests
{
    [Theory]
    [InlineData(UuidVersion.V4, 4)]
    [InlineData(UuidVersion.V7, 7)]
    public void Generate_ProducesUniqueUuidsOfRequestedVersion(UuidVersion version, int expectedVersion)
    {
        var ids = UuidTools.Generate(version, 500, new UuidFormat()).Value;

        Assert.Equal(500, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Equal(expectedVersion, UuidTools.Inspect(id).Value.Version));
    }

    [Fact]
    public void GenerateV7_TimestampsNeverGoBackwards()
    {
        var timestamps = UuidTools.Generate(UuidVersion.V7, 200, new UuidFormat()).Value
            .Select(id => UuidTools.Inspect(id).Value.Timestamp!.Value)
            .ToList();

        Assert.Equal(timestamps.Order(), timestamps);
    }

    [Theory]
    [InlineData(false, true, false, "550e8400-e29b-41d4-a716-446655440000")]
    [InlineData(true, true, false, "550E8400-E29B-41D4-A716-446655440000")]
    [InlineData(false, false, false, "550e8400e29b41d4a716446655440000")]
    [InlineData(false, true, true, "{550e8400-e29b-41d4-a716-446655440000}")]
    public void Format_AppliesOptions(bool upper, bool hyphens, bool braces, string expected) =>
        Assert.Equal(expected, UuidTools.Format(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), new UuidFormat(upper, hyphens, braces)));

    [Theory]
    [InlineData(0)]
    [InlineData(UuidTools.MaxCount + 1)]
    public void Generate_RejectsBadCount(int count) =>
        Assert.Equal("uuid.count", UuidTools.Generate(UuidVersion.V4, count, new UuidFormat()).Error?.Code);

    [Fact]
    public void Inspect_V7_ReturnsEmbeddedTimestamp()
    {
        var info = UuidTools.Inspect("019a0c3e-1c00-7000-8000-000000000000").Value;

        Assert.Equal(7, info.Version);
        Assert.Equal("RFC 9562 / RFC 4122", info.Variant);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(0x019a0c3e1c00), info.Timestamp);
    }

    [Fact]
    public void Inspect_V1_ReturnsGregorianTimestamp()
    {
        // Test vector from RFC 9562 appendix A.1: 2022-02-22 19:22:22 UTC.
        var info = UuidTools.Inspect("C232AB00-9414-11EC-B3C8-9F6BDECED846").Value;

        Assert.Equal(1, info.Version);
        Assert.Equal(new DateTimeOffset(2022, 2, 22, 19, 22, 22, TimeSpan.Zero), info.Timestamp);
    }

    [Theory]
    [InlineData("{550E8400-E29B-41D4-A716-446655440000}")]
    [InlineData("urn:uuid:550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("550e8400e29b41d4a716446655440000")]
    public void Inspect_AcceptsCommonNotations(string input)
    {
        var info = UuidTools.Inspect(input).Value;

        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", info.Canonical);
        Assert.Equal(4, info.Version);
        Assert.Null(info.Timestamp);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", true, false)]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff", false, true)]
    public void Inspect_RecognizesNilAndMax(string input, bool nil, bool max)
    {
        var info = UuidTools.Inspect(input).Value;
        Assert.Equal(nil, info.IsNil);
        Assert.Equal(max, info.IsMax);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("550e8400-e29b-41d4-a716-44665544000")]
    public void Inspect_Invalid_IsAnError(string input) =>
        Assert.Equal("uuid.invalid", UuidTools.Inspect(input).Error?.Code);
}

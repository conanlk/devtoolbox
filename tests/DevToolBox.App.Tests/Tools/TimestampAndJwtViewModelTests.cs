using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools.Jwt;
using DevToolBox.App.Tools.Timestamp;
using DevToolBox.Core.Tools.Timestamp;

namespace DevToolBox.App.Tests.Tools;

public class TimestampViewModelTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 7, 30, 0, TimeSpan.Zero));

    private TimestampViewModel Create()
    {
        var vm = new TimestampViewModel(_time);
        vm.SelectedZone = vm.TimeZones.Single(z => z.Zone.Id == TimeZoneInfo.Utc.Id);
        return vm;
    }

    [Fact]
    public void StartsAtNow()
    {
        using var vm = Create();

        Assert.Equal("1792049400", vm.EpochInput);
        Assert.Equal("1792049400", vm.CurrentEpochSeconds);
        Assert.Contains(vm.EpochFields, f => f.Label == "Relative" && f.Value == "just now");
    }

    [Fact]
    public void EpochInput_DetectsMillisecondsAndDescribes()
    {
        using var vm = Create();

        vm.EpochInput = "1792049400000";

        Assert.Equal("Interpreted as milliseconds", vm.DetectedUnit);
        Assert.Contains(vm.EpochFields, f => f.Label == "UTC" && f.Value == "2026-10-15 07:30:00.000 UTC");
    }

    [Fact]
    public void ExplicitUnit_AndInvalidInput()
    {
        using var vm = Create();
        vm.SelectedUnit = vm.Units.Single(u => u.Value == TimestampUnit.Milliseconds);

        vm.EpochInput = "1000";
        Assert.Contains(vm.EpochFields, f => f.Label == "UTC" && f.Value == "1970-01-01 00:00:01.000 UTC");

        vm.EpochInput = "abc";
        Assert.NotNull(vm.EpochError);
        Assert.Empty(vm.EpochFields);
    }

    [Fact]
    public void DateInput_UsesSelectedZone()
    {
        using var vm = Create();
        vm.SelectedZone = vm.TimeZones.Single(z => z.Zone.Id == "Asia/Ho_Chi_Minh");

        vm.DateInput = "2026-10-15 14:30:00";

        Assert.Contains(vm.DateFields, f => f.Label == "Unix seconds" && f.Value == "1792049400");
        Assert.Contains(vm.DateFields, f => f.Label == "ISO 8601 (UTC)" && f.Value == "2026-10-15T07:30:00.000Z");
    }

    [Fact]
    public void Clock_TicksEverySecond()
    {
        using var vm = Create();

        _time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal("1792049403", vm.CurrentEpochSeconds);
    }

    [Fact]
    public void TimeZones_StartWithLocalAndUtc()
    {
        using var vm = new TimestampViewModel(_time);

        Assert.Same(TimeZoneInfo.Local, vm.TimeZones[0].Zone);
        Assert.Same(TimeZoneInfo.Utc, vm.TimeZones[1].Zone);
        Assert.True(vm.TimeZones.Count > 50);
    }
}

public class JwtViewModelTests
{
    private readonly ManualTimeProvider _time = new();

    private JwtViewModel CreateWithSample()
    {
        var vm = new JwtViewModel(_time, TimeSpan.FromHours(1));
        vm.LoadSampleCommand.Execute(null);
        vm.Decode();
        return vm;
    }

    [Fact]
    public void Sample_DecodesAndVerifies()
    {
        using var vm = CreateWithSample();

        Assert.True(vm.HasToken);
        Assert.Contains("\"alg\": \"HS256\"", vm.HeaderJson, StringComparison.Ordinal);
        Assert.Contains("John Doe", vm.PayloadJson, StringComparison.Ordinal);
        Assert.Equal(BadgeKind.Warning, vm.StatusKind);
        Assert.Equal("Signature verified", vm.VerificationText);
        Assert.Equal(BadgeKind.Success, vm.VerificationKind);
        Assert.Contains(vm.Claims, c => c.Label == "Issued at (iat)");
    }

    [Fact]
    public void WrongSecret_IsInvalid()
    {
        using var vm = CreateWithSample();

        vm.Secret = "nope";

        Assert.Equal("Invalid signature", vm.VerificationText);
        Assert.Equal(BadgeKind.Error, vm.VerificationKind);
    }

    [Fact]
    public void Malformed_ShowsError_AndHidesDetails()
    {
        using var vm = new JwtViewModel(_time, TimeSpan.FromHours(1)) { Token = "abc.def" };

        vm.Decode();

        Assert.False(vm.HasToken);
        Assert.Contains("3 parts", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(vm.PayloadJson);
    }

    [Fact]
    public void ExpiredToken_IsFlagged()
    {
        var exp = _time.GetUtcNow().AddHours(-2).ToUnixTimeSeconds();
        var payload = System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes($"{{\"exp\":{exp}}}"));
        using var vm = new JwtViewModel(_time, TimeSpan.FromHours(1)) { Token = $"eyJhbGciOiJIUzI1NiJ9.{payload}.sig" };

        vm.Decode();

        Assert.Equal(BadgeKind.Error, vm.StatusKind);
        Assert.Equal("Expired 2 hours ago", vm.StatusText);
    }
}

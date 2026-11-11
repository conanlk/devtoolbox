using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.App.Shared.Controls;
using DevToolBox.Core.Tools.Jwt;
using DevToolBox.Core.Tools.Timestamp;

namespace DevToolBox.App.Tools.Jwt;

public enum BadgeKind
{
    None,
    Success,
    Warning,
    Error,
}

public sealed record JwtClaimRow(string Key, string Label, string Value);

public sealed partial class JwtViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    public const string SampleToken =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private readonly TimeProvider _time;
    private readonly Debouncer _debouncer;
    private JwtToken? _token;

    public JwtViewModel(TimeProvider time, TimeSpan? debounce = null)
    {
        _time = time;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
    }

    [ObservableProperty]
    public partial string Token { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HeaderJson { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PayloadJson { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    [ObservableProperty]
    public partial BadgeKind StatusKind { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<LabeledValue> Claims { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasToken { get; private set; }

    /// <summary>The token split into header / payload / signature for color-coded display.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TextSegment> TokenSegments { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaims))]
    public partial IReadOnlyList<JwtClaimRow> ClaimRows { get; private set; } = [];

    public bool HasClaims => ClaimRows.Count > 0;

    /// <summary>"HMAC-SHA256", or the algorithm name when it can't be verified here.</summary>
    [ObservableProperty]
    public partial string? AlgorithmLabel { get; private set; }

    /// <summary>"HS256 · JWT · 3 registered claims" for the status bar.</summary>
    [ObservableProperty]
    public partial string? SummaryText { get; private set; }

    // ----- Signature verification (optional, HMAC only) -----

    [ObservableProperty]
    public partial string Secret { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SecretIsBase64 { get; set; }

    [ObservableProperty]
    public partial string? VerificationText { get; private set; }

    [ObservableProperty]
    public partial BadgeKind VerificationKind { get; private set; }

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
    }

    /// <summary>Decodes the current token immediately.</summary>
    public void Decode()
    {
        _token = null;
        ErrorMessage = null;
        HeaderJson = PayloadJson = string.Empty;
        Claims = [];
        ClaimRows = [];
        TokenSegments = [];
        AlgorithmLabel = SummaryText = null;
        StatusText = null;
        StatusKind = BadgeKind.None;

        if (!string.IsNullOrWhiteSpace(Token))
        {
            var now = _time.GetUtcNow();
            var result = JwtDecoder.Decode(Token, now);
            if (result.IsOk)
            {
                _token = result.Value;
                HeaderJson = _token.HeaderJson;
                PayloadJson = _token.PayloadJson;
                Claims = BuildClaims(_token, now);
                (StatusText, StatusKind) = DescribeStatus(_token, now);
                var (header, payload, signature) = _token.Parts;
                TokenSegments =
                [
                    new(header, "Brush.Jwt.Header"),
                    new(".", "Brush.TextFaint"),
                    new(payload, "Brush.Jwt.Payload"),
                    new(".", "Brush.TextFaint"),
                    new(signature, "Brush.Jwt.Signature"),
                ];
                ClaimRows = [.. _token.RegisteredClaims.Select(c => new JwtClaimRow(c.Name, c.Description,
                    c.Time is { } at ? $"{at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)} · {RelativeTime.Format(at, now)}" : c.RawValue))];
                AlgorithmLabel = _token.Algorithm?.ToUpperInvariant() switch
                {
                    "HS256" => "HMAC-SHA256",
                    "HS384" => "HMAC-SHA384",
                    "HS512" => "HMAC-SHA512",
                    var other => other,
                };
                SummaryText = Localizer.Format(Strings.Jwt_Summary, _token.Algorithm ?? Strings.Jwt_NoAlg, _token.Type ?? Strings.Jwt_NoTyp,
                    Localizer.Format(ClaimRows.Count == 1 ? Strings.Jwt_RegisteredClaim : Strings.Jwt_RegisteredClaims, ClaimRows.Count));
            }
            else
            {
                ErrorMessage = result.Error.Message;
            }
        }

        HasToken = _token is not null;
        Verify();
    }

    [RelayCommand]
    private void LoadSample()
    {
        Token = SampleToken;
        Secret = "your-256-bit-secret";
    }

    protected override void OnLanguageChanged() => Decode();

    partial void OnTokenChanged(string value) => _debouncer.Run(Decode);

    partial void OnSecretChanged(string value) => Verify();

    partial void OnSecretIsBase64Changed(bool value) => Verify();

    private void Verify()
    {
        VerificationText = null;
        VerificationKind = BadgeKind.None;
        if (_token is null || string.IsNullOrEmpty(Secret))
        {
            return;
        }

        var result = JwtSignatureVerifier.VerifyHmac(_token, Secret, SecretIsBase64);
        (VerificationText, VerificationKind) = !result.IsOk
            ? (result.Error.Message, BadgeKind.Warning)
            : result.Value
                ? (Strings.Jwt_SignatureVerified, BadgeKind.Success)
                : (Strings.Jwt_InvalidSignature, BadgeKind.Error);
    }

    private static (string, BadgeKind) DescribeStatus(JwtToken token, DateTimeOffset now)
    {
        var exp = token.TimeClaims.FirstOrDefault(c => c.Name == "exp")?.Value;
        var nbf = token.TimeClaims.FirstOrDefault(c => c.Name == "nbf")?.Value;
        var unsecured = token.IsUnsecured ? Strings.Jwt_Unsigned : string.Empty;
        return token.Status switch
        {
            JwtStatus.Expired => (Localizer.Format(Strings.Jwt_StatusExpired, RelativeTime.Format(exp!.Value, now)) + unsecured, BadgeKind.Error),
            JwtStatus.NotYetValid => (Localizer.Format(Strings.Jwt_StatusNotYetValid, RelativeTime.Format(nbf!.Value, now)) + unsecured, BadgeKind.Warning),
            JwtStatus.Valid => (Localizer.Format(Strings.Jwt_StatusValid, RelativeTime.Format(exp!.Value, now)) + unsecured,
                token.IsUnsecured ? BadgeKind.Warning : BadgeKind.Success),
            _ => (Strings.Jwt_StatusNoExpiry + unsecured, BadgeKind.Warning),
        };
    }

    private static List<LabeledValue> BuildClaims(JwtToken token, DateTimeOffset now)
    {
        var rows = new List<LabeledValue> { new(Strings.Jwt_Algorithm, token.Algorithm ?? Strings.Jwt_None) };
        if (token.Type is { } type)
        {
            rows.Add(new(Strings.Jwt_Type, type));
        }

        foreach (var claim in token.TimeClaims)
        {
            var value = claim.Value is { } at
                ? $"{at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)} ({RelativeTime.Format(at, now)}) · {claim.RawValue}"
                : Localizer.Format(Strings.Jwt_OutOfRange, claim.RawValue);
            rows.Add(new($"{claim.Description} ({claim.Name})", value));
        }

        return rows;
    }

    public void AcceptInput(string text, string? intent)
    {
        Token = text;
        Decode();
    }
}

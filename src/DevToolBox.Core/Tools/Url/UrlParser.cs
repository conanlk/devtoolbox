using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Url;

public sealed record QueryParameter(string Key, string Value);

public sealed record ParsedUrl(
    string Scheme,
    string UserInfo,
    string Host,
    int? Port,
    string Path,
    IReadOnlyList<QueryParameter> Query,
    string Fragment);

/// <summary>Splits an absolute URL into its parts and rebuilds it after edits.</summary>
public static class UrlParser
{
    public static Result<ParsedUrl> Parse(string? url)
    {
        var trimmed = url?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Scheme))
        {
            return new Error("url.not-absolute", CoreStrings.Url_NotAbsolute);
        }

        return new ParsedUrl(
            uri.Scheme,
            uri.UserInfo,
            uri.Host,
            uri.IsDefaultPort ? null : uri.Port,
            uri.AbsolutePath,
            ParseQuery(uri.Query),
            uri.Fragment.TrimStart('#'));
    }

    /// <summary>Parses "a=1&amp;b=two+words" (leading '?' optional), decoding keys and values.</summary>
    public static IReadOnlyList<QueryParameter> ParseQuery(string? query)
    {
        var text = (query ?? string.Empty).TrimStart('?');
        if (text.Length == 0)
        {
            return [];
        }

        return [.. text.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            return new QueryParameter(DecodeOrRaw(key), DecodeOrRaw(value));
        })];
    }

    public static string Build(ParsedUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var builder = new StringBuilder();
        builder.Append(url.Scheme).Append("://");
        if (url.UserInfo.Length > 0)
        {
            builder.Append(url.UserInfo).Append('@');
        }

        builder.Append(url.Host);
        if (url.Port is { } port)
        {
            builder.Append(':').Append(port);
        }

        builder.Append(url.Path);
        var query = string.Join('&', url.Query
            .Where(p => p.Key.Length > 0 || p.Value.Length > 0)
            .Select(p => $"{EncodeComponent(p.Key)}={EncodeComponent(p.Value)}"));
        if (query.Length > 0)
        {
            builder.Append('?').Append(query);
        }

        if (url.Fragment.Length > 0)
        {
            builder.Append('#').Append(url.Fragment);
        }

        return builder.ToString();
    }

    private static string DecodeOrRaw(string text)
    {
        var decoded = UrlCodec.Decode(text, UrlEncodeMode.Component, plusAsSpace: true);
        return decoded.IsOk ? decoded.Value : text;
    }

    private static string EncodeComponent(string text)
    {
        var encoded = UrlCodec.Encode(text, UrlEncodeMode.Component);
        return encoded.IsOk ? encoded.Value : text;
    }
}

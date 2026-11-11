using System.Text.RegularExpressions;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.RegularExpressions;

public sealed record RegexGroupInfo(int Number, string Name, bool Success, int Index, int Length, string Value);

public sealed record RegexMatchInfo(int Index, int Length, string Value, IReadOnlyList<RegexGroupInfo> Groups);

public sealed record RegexRunResult(
    IReadOnlyList<RegexMatchInfo> Matches,
    bool IsTruncated,
    IReadOnlyList<string> GroupNames,
    string? ReplaceResult);

/// <summary>.NET regular expressions with a hard timeout so catastrophic backtracking can't hang the app.</summary>
public static class RegexTester
{
    public const int DefaultMaxMatches = 1000;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public static Result<RegexRunResult> Run(
        string pattern,
        string? input,
        RegexOptions options,
        string? replacement = null,
        TimeSpan? timeout = null,
        int maxMatches = DefaultMaxMatches)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        input ??= string.Empty;
        var limit = timeout ?? DefaultTimeout;

        Regex regex;
        try
        {
            regex = new Regex(pattern, options, limit);
        }
        catch (RegexParseException ex)
        {
            return new Error("regex.invalid", $"{ex.Error}: {CleanMessage(ex.Message)}")
            {
                Location = new ErrorLocation(Offset: ex.Offset),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new Error("regex.unsupported", CleanMessage(ex.Message));
        }

        try
        {
            var matches = new List<RegexMatchInfo>();
            var groupNumbers = regex.GetGroupNumbers();
            var match = regex.Match(input);
            while (match.Success && matches.Count < maxMatches)
            {
                matches.Add(new RegexMatchInfo(match.Index, match.Length, match.Value, [.. groupNumbers.Skip(1).Select(n =>
                {
                    var group = match.Groups[n];
                    return new RegexGroupInfo(n, regex.GroupNameFromNumber(n), group.Success, group.Index, group.Length, group.Value);
                })]));
                match = match.NextMatch();
            }

            var replaced = replacement is null ? null : regex.Replace(input, replacement);
            return new RegexRunResult(
                matches,
                match.Success,
                [.. groupNumbers.Skip(1).Select(regex.GroupNameFromNumber)],
                replaced);
        }
        catch (RegexMatchTimeoutException)
        {
            return new Error("regex.timeout",
                CoreLocalization.Format(CoreStrings.Regex_Timeout, limit.TotalSeconds));
        }
        catch (ArgumentException ex)
        {
            return new Error("regex.replacement", CleanMessage(ex.Message));
        }
    }

    private static string CleanMessage(string message)
    {
        // "Invalid pattern 'x' at offset 3. Not enough )'s." → keep the useful sentence.
        var lastSentence = message.LastIndexOf(". ", StringComparison.Ordinal);
        return lastSentence > 0 && lastSentence < message.Length - 2 ? message[(lastSentence + 2)..].Trim() : message.Trim();
    }
}

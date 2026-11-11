namespace DevToolBox.Core.Shared;

/// <summary>
/// A user-facing failure returned by tool logic instead of throwing into the UI.
/// </summary>
/// <param name="Code">Stable machine-readable identifier, e.g. "json.invalid".</param>
/// <param name="Message">Human-readable message shown inline in the tool.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Where in the input the problem is, when known (e.g. JSON line/column, regex offset).</summary>
    public ErrorLocation? Location { get; init; }
}

/// <summary>1-based line and column, and/or a 0-based character offset into the input.</summary>
public sealed record ErrorLocation(int? Line = null, int? Column = null, int? Offset = null);

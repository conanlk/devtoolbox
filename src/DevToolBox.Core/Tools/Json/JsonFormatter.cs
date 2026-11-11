using DevToolBox.Core.Shared;
using DevToolBox.Core.Shared.Json;

namespace DevToolBox.Core.Tools.Json;

public sealed record JsonFormatOptions(JsonIndent Indent = JsonIndent.TwoSpaces, bool SortKeys = false);

public sealed record JsonFormatResult(string Text, JsonTreeNode Root, int NodeCount, int MaxDepth);

/// <summary>Validates, beautifies or minifies JSON without changing any value.</summary>
public static class JsonFormatter
{
    public static Result<JsonFormatResult> Format(string? json, JsonFormatOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var parsed = JsonTreeParser.Parse(json, cancellationToken);
        if (!parsed.IsOk)
        {
            return parsed.Error;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var text = JsonTreeWriter.Write(parsed.Value.Root, options.Indent, options.SortKeys);
        return new JsonFormatResult(text, parsed.Value.Root, parsed.Value.NodeCount, parsed.Value.MaxDepth);
    }
}

using System.Text;
using System.Text.Json;
using DevToolBox.Core.Localization;

namespace DevToolBox.Core.Shared.Json;

public sealed record JsonParseResult(JsonTreeNode Root, int NodeCount, int MaxDepth);

/// <summary>Parses JSON into a <see cref="JsonTreeNode"/> tree with precise error locations.</summary>
public static class JsonTreeParser
{
    public const int MaxDepth = 1000;

    public static Result<JsonParseResult> Parse(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Error("json.empty", CoreStrings.Json_Empty);
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            return ParseBytes(bytes, cancellationToken);
        }
        catch (JsonException ex)
        {
            return ToError(ex, bytes);
        }
    }

    private static JsonParseResult ParseBytes(byte[] bytes, CancellationToken cancellationToken)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = MaxDepth });
        var stack = new Stack<JsonTreeNode>();
        JsonTreeNode? root = null;
        (string Raw, string Decoded)? pendingKey = null;
        var count = 0;
        var maxDepth = 0;

        while (reader.Read())
        {
            if ((++count & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            JsonTreeNode? node = null;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    pendingKey = (Encoding.UTF8.GetString(reader.ValueSpan), reader.GetString()!);
                    count--;
                    continue;
                case JsonTokenType.StartObject:
                    node = JsonTreeNode.NewObject();
                    break;
                case JsonTokenType.StartArray:
                    node = JsonTreeNode.NewArray();
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    stack.Pop();
                    count--;
                    continue;
                case JsonTokenType.String:
                    node = JsonTreeNode.Scalar(JsonNodeKind.String, "\"" + Encoding.UTF8.GetString(reader.ValueSpan) + "\"");
                    break;
                case JsonTokenType.Number:
                    node = JsonTreeNode.Scalar(JsonNodeKind.Number, Encoding.UTF8.GetString(reader.ValueSpan));
                    break;
                case JsonTokenType.True:
                    node = JsonTreeNode.Scalar(JsonNodeKind.True, "true");
                    break;
                case JsonTokenType.False:
                    node = JsonTreeNode.Scalar(JsonNodeKind.False, "false");
                    break;
                case JsonTokenType.Null:
                    node = JsonTreeNode.Scalar(JsonNodeKind.Null, "null");
                    break;
                default:
                    count--;
                    continue;
            }

            if (stack.TryPeek(out var parent))
            {
                if (parent.Kind == JsonNodeKind.Object)
                {
                    var (raw, decoded) = pendingKey!.Value;
                    parent.Properties!.Add(new JsonTreeProperty(raw, decoded, node));
                }
                else
                {
                    parent.Items!.Add(node);
                }
            }
            else
            {
                root = node;
            }

            if (node.IsContainer)
            {
                stack.Push(node);
                maxDepth = Math.Max(maxDepth, stack.Count);
            }
        }

        return new JsonParseResult(root!, count, maxDepth);
    }

    private static Error ToError(JsonException ex, byte[] bytes)
    {
        var line = (int)(ex.LineNumber ?? 0);
        var bytePosition = (int)(ex.BytePositionInLine ?? 0);
        var column = ColumnFromBytePosition(bytes, line, bytePosition);
        var reason = CleanMessage(ex.Message);
        return new Error("json.invalid", CoreLocalization.Format(CoreStrings.Json_Invalid, line + 1, column, reason))
        {
            Location = new ErrorLocation(line + 1, column),
        };
    }

    private static int ColumnFromBytePosition(byte[] bytes, int line, int bytePosition)
    {
        var lineStart = 0;
        for (var i = 0; i < bytes.Length && line > 0; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                line--;
                lineStart = i + 1;
            }
        }

        var length = Math.Clamp(bytePosition, 0, bytes.Length - lineStart);
        return Encoding.UTF8.GetCharCount(bytes, lineStart, length) + 1;
    }

    private static string CleanMessage(string message)
    {
        foreach (var marker in new[] { " LineNumber:", " Path:" })
        {
            var index = message.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                message = message[..index];
            }
        }

        return message.Trim();
    }
}

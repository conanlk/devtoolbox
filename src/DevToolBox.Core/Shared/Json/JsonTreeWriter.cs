using System.Text;

namespace DevToolBox.Core.Shared.Json;

public enum JsonIndent
{
    TwoSpaces,
    FourSpaces,
    Tab,
    Minified,
}

/// <summary>Writes a <see cref="JsonTreeNode"/> as text, preserving every scalar exactly.</summary>
public static class JsonTreeWriter
{
    public static string Write(JsonTreeNode root, JsonIndent indent, bool sortKeys = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        var builder = new StringBuilder();
        var unit = indent switch
        {
            JsonIndent.TwoSpaces => "  ",
            JsonIndent.FourSpaces => "    ",
            JsonIndent.Tab => "\t",
            _ => null,
        };
        WriteNode(builder, root, unit, 0, sortKeys);
        return builder.ToString();
    }

    private static void WriteNode(StringBuilder builder, JsonTreeNode node, string? unit, int depth, bool sortKeys)
    {
        switch (node.Kind)
        {
            case JsonNodeKind.Object:
                var properties = sortKeys
                    ? node.Properties!.OrderBy(p => p.Key, StringComparer.Ordinal).ToList()
                    : node.Properties!;
                WriteContainer(builder, '{', '}', properties.Count, unit, depth, i =>
                {
                    builder.Append('"').Append(properties[i].RawKey).Append('"');
                    builder.Append(unit is null ? ":" : ": ");
                    WriteNode(builder, properties[i].Value, unit, depth + 1, sortKeys);
                });
                break;
            case JsonNodeKind.Array:
                var items = node.Items!;
                WriteContainer(builder, '[', ']', items.Count, unit, depth, i => WriteNode(builder, items[i], unit, depth + 1, sortKeys));
                break;
            default:
                builder.Append(node.RawValue);
                break;
        }
    }

    private static void WriteContainer(StringBuilder builder, char open, char close, int count, string? unit, int depth, Action<int> writeChild)
    {
        builder.Append(open);
        if (count == 0)
        {
            builder.Append(close);
            return;
        }

        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            NewLine(builder, unit, depth + 1);
            writeChild(i);
        }

        NewLine(builder, unit, depth);
        builder.Append(close);
    }

    private static void NewLine(StringBuilder builder, string? unit, int depth)
    {
        if (unit is null)
        {
            return;
        }

        builder.Append('\n');
        for (var i = 0; i < depth; i++)
        {
            builder.Append(unit);
        }
    }
}

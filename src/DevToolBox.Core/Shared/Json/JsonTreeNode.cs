namespace DevToolBox.Core.Shared.Json;

public enum JsonNodeKind
{
    Object,
    Array,
    String,
    Number,
    True,
    False,
    Null,
}

/// <summary>A JSON property: the key exactly as written (escaped, without quotes) and decoded.</summary>
public sealed record JsonTreeProperty(string RawKey, string Key, JsonTreeNode Value);

/// <summary>
/// Lightweight JSON tree that keeps every scalar exactly as written in the source — number literals
/// are never rounded (e.g. 64-bit ids) and string escapes are preserved.
/// </summary>
public sealed class JsonTreeNode
{
    private JsonTreeNode(JsonNodeKind kind, string? raw, List<JsonTreeProperty>? properties, List<JsonTreeNode>? items)
    {
        Kind = kind;
        RawValue = raw;
        Properties = properties;
        Items = items;
    }

    public JsonNodeKind Kind { get; }

    /// <summary>Source text for scalars (strings include their quotes); null for objects and arrays.</summary>
    public string? RawValue { get; }

    public List<JsonTreeProperty>? Properties { get; }

    public List<JsonTreeNode>? Items { get; }

    public int ChildCount => Properties?.Count ?? Items?.Count ?? 0;

    public bool IsContainer => Kind is JsonNodeKind.Object or JsonNodeKind.Array;

    public static JsonTreeNode NewObject() => new(JsonNodeKind.Object, null, [], null);

    public static JsonTreeNode NewArray() => new(JsonNodeKind.Array, null, null, []);

    public static JsonTreeNode Scalar(JsonNodeKind kind, string raw) => new(kind, raw, null, null);
}

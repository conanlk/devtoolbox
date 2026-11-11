namespace DevToolBox.App.Tools;

public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> All { get; }

    ToolDefinition? Find(string id);
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _byId;

    public ToolRegistry(IEnumerable<ToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        All = [.. tools];
        _byId = new Dictionary<string, ToolDefinition>(StringComparer.Ordinal);
        foreach (var tool in All)
        {
            if (!_byId.TryAdd(tool.Id, tool))
            {
                throw new ArgumentException($"Duplicate tool id '{tool.Id}'.", nameof(tools));
            }
        }
    }

    public IReadOnlyList<ToolDefinition> All { get; }

    public ToolDefinition? Find(string id) => _byId.GetValueOrDefault(id);
}

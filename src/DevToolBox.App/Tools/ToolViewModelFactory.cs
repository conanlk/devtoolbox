namespace DevToolBox.App.Tools;

public interface IToolViewModelFactory
{
    ToolViewModelBase Create(ToolDefinition tool);
}

public sealed class ServiceProviderToolViewModelFactory(IServiceProvider services) : IToolViewModelFactory
{
    public ToolViewModelBase Create(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.CreateViewModel(services);
    }
}

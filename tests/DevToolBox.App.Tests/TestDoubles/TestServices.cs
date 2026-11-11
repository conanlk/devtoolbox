using DevToolBox.App.Platform;
using DevToolBox.App.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolBox.App.Tests.TestDoubles;

/// <summary>The real composition root with platform services swapped for fakes.</summary>
internal sealed class TestServices : IDisposable
{
    public TestServices(IReadOnlyList<ToolDefinition>? tools = null)
    {
        Provider = CompositionRoot.Build(Settings, tools ?? ToolCatalog.All, services =>
        {
            services.AddSingleton<IClipboardService>(Clipboard);
            services.AddSingleton<IFileService>(Files);
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public InMemorySettingsStore Settings { get; } = new();

    public FakeClipboardService Clipboard { get; } = new();

    public FakeFileService Files { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public ServiceProvider Provider { get; }

    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public void Dispose() => Provider.Dispose();
}

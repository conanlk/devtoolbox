using Avalonia.Controls;

namespace DevToolBox.App.Platform;

/// <summary>Gives services access to the window that owns clipboard and file dialogs.</summary>
public interface ITopLevelProvider
{
    TopLevel? TopLevel { get; }
}

public sealed class TopLevelProvider : ITopLevelProvider
{
    public TopLevel? TopLevel { get; set; }
}

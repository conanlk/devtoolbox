using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DevToolBox.App.Shell;

/// <summary>
/// AOT-safe view lookup for tool pages: the view comes from the tool's own definition (no reflection)
/// and is cached so switching tools keeps scroll position, selection and other view state.
/// </summary>
public sealed class ToolPageTemplate : IDataTemplate
{
    private readonly Dictionary<string, Control> _views = new(StringComparer.Ordinal);

    public bool Match(object? data) => data is ToolPageViewModel;

    public Control? Build(object? param)
    {
        if (param is not ToolPageViewModel page)
        {
            return null;
        }

        if (!_views.TryGetValue(page.Tool.Id, out var view))
        {
            view = page.Tool.CreateView();
            view.DataContext = page.ToolViewModel;
            _views[page.Tool.Id] = view;
        }

        return view;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools;

/// <summary>Base class for every tool's view model.</summary>
public abstract class ToolViewModelBase : ObservableObject, ILanguageAware
{
    protected ToolViewModelBase() => Localizer.Register(this);

    void ILanguageAware.OnLanguageChanged() => OnLanguageChanged();

    /// <summary>
    /// The UI language changed: rebuild any text the view model composed itself (status lines, errors from Core,
    /// labeled rows). Text in the view's XAML updates on its own.
    /// </summary>
    protected virtual void OnLanguageChanged()
    {
    }

    /// <summary>Call from Dispose: a disposed view model must not rebuild text from released resources.</summary>
    protected void StopLanguageUpdates() => Localizer.Unregister(this);
}

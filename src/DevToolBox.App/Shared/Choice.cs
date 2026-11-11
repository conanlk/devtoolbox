using CommunityToolkit.Mvvm.ComponentModel;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Shared;

/// <summary>Anything shown by its <see cref="Label"/> (App.axaml has one DataTemplate for all of them).</summary>
public interface ILabeled
{
    string Label { get; }
}

/// <summary>
/// An option shown in a ComboBox: a label for people, a value for the view model. A localized label is read
/// through a delegate and re-announced when the language changes, so the open list and the selection update live.
/// </summary>
public sealed class Choice<T> : ObservableObject, ILabeled, ILanguageAware
{
    private readonly Func<string> _label;

    /// <summary>A localized label, e.g. <c>() =&gt; Strings.Hash_Format_Lowercase</c>.</summary>
    public Choice(Func<string> label, T value)
    {
        ArgumentNullException.ThrowIfNull(label);
        _label = label;
        Value = value;
        Localizer.Register(this);
    }

    /// <summary>A label that is the same in every language, e.g. "Base64" or "SHA-256".</summary>
    public Choice(string label, T value)
    {
        _label = () => label;
        Value = value;
    }

    public string Label => _label();

    public T Value { get; }

    void ILanguageAware.OnLanguageChanged() => OnPropertyChanged(nameof(Label));

    public override string ToString() => Label;
}

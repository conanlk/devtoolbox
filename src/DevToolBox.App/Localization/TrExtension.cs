using Avalonia;
using Avalonia.Data;

namespace DevToolBox.App.Localization;

/// <summary>
/// XAML: <c>Text="{l:Tr Home_Title}"</c>. Binds to the localized resource string and updates when the language
/// changes. Observable-backed, so no reflection (Native-AOT safe).
/// </summary>
public sealed class TrExtension(string key)
{
    public string Key { get; set; } = key;

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => Localizer.Observe(Key).ToBinding();
}

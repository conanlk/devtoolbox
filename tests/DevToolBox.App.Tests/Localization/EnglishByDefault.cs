using System.Runtime.CompilerServices;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Tests.Localization;

internal static class EnglishByDefault
{
    /// <summary>Tests assert English text whatever the machine's UI language; language tests switch explicitly.</summary>
    [ModuleInitializer]
    internal static void Pin() => Localizer.SetLanguage(AppLanguage.English);
}

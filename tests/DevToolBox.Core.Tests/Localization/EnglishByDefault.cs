using System.Globalization;
using System.Runtime.CompilerServices;
using DevToolBox.Core.Localization;

namespace DevToolBox.Core.Tests.Localization;

internal static class EnglishByDefault
{
    /// <summary>Core messages are asserted in English whatever the machine's UI language.</summary>
    [ModuleInitializer]
    internal static void Pin() => CoreLocalization.Culture = CultureInfo.GetCultureInfo("en");
}

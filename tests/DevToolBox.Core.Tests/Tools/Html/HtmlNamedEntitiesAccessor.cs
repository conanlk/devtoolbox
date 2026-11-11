using System.Reflection;

namespace DevToolBox.Core.Tests.Tools.Html;

/// <summary>Reads the internal entity table so the test can validate every name it contains.</summary>
internal static class HtmlNamedEntitiesAccessor
{
    public static IEnumerable<KeyValuePair<int, string>> All =>
        (IEnumerable<KeyValuePair<int, string>>)typeof(DevToolBox.Core.Tools.Html.HtmlEntityCodec).Assembly
            .GetType("DevToolBox.Core.Tools.Html.HtmlNamedEntities")!
            .GetProperty("All", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
}

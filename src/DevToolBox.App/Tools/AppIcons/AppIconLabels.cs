using DevToolBox.App.Localization;

namespace DevToolBox.App.Tools.AppIcons;

/// <summary>
/// Shows Core's English file descriptions ("Settings · 29pt @2x", "Launcher (round)") in the UI language.
/// Core keeps them English because they double as stable data in the icon catalog.
/// </summary>
public static class AppIconLabels
{
    public static string Localize(string use)
    {
        ArgumentNullException.ThrowIfNull(use);
        var separator = use.IndexOf(" · ", StringComparison.Ordinal);
        var head = separator < 0 ? use : use[..separator];
        var tail = separator < 0 ? string.Empty : use[separator..];
        return Translate(head) + tail;
    }

    private static string Translate(string text) => text switch
    {
        "Notification" => Strings.AppIcons_Use_Notification,
        "Settings" => Strings.AppIcons_Use_Settings,
        "Spotlight" => Strings.AppIcons_Use_Spotlight,
        "App" => Strings.AppIcons_Use_App,
        "App (iPad Pro)" => Strings.AppIcons_Use_AppIpadPro,
        "App Store" => Strings.AppIcons_Use_AppStore,
        "Launcher" => Strings.AppIcons_Use_Launcher,
        "Launcher (round)" => Strings.AppIcons_Use_LauncherRound,
        "Adaptive foreground" => Strings.AppIcons_Use_AdaptiveForeground,
        "Play Store listing" => Strings.AppIcons_Use_PlayStore,
        "Asset catalog manifest" => Strings.AppIcons_Use_AssetCatalog,
        "Adaptive icon definition" => Strings.AppIcons_Use_AdaptiveDefinition,
        "Adaptive round icon definition" => Strings.AppIcons_Use_AdaptiveRoundDefinition,
        "Adaptive background colour" => Strings.AppIcons_Use_AdaptiveBackground,
        _ => text,
    };
}

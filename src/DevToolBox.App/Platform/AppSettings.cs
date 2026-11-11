using System.Text.Json.Serialization;
using DevToolBox.App.Localization;

namespace DevToolBox.App.Platform;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>App-level settings persisted between sessions. Per-tool settings are added in later milestones.</summary>
public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>UI language the user picked; null until they pick one (then the OS language is used).</summary>
    public AppLanguage? Language { get; set; }

    public List<string> FavoriteToolIds { get; set; } = [];

    public bool IsSidebarCollapsed { get; set; }

    public WindowPlacement? Window { get; set; }

    /// <summary>"Open at login" from the tray menu; null until the default has been applied once.</summary>
    public bool? LaunchAtLogin { get; set; }
}

public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool IsMaximized);

/// <summary>Source-generated serializer: no reflection, Native-AOT safe.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using DevToolBox.App.Platform;
using DevToolBox.App.Shared.Controls;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Shared;

public class ThemeAndIconTests
{
    [AvaloniaFact]
    public void AllIconResources_AreValidGeometries()
    {
        var icons = new ResourceInclude(new Uri("avares://DevToolBox/"))
        {
            Source = new Uri("avares://DevToolBox/Themes/Icons.axaml"),
        }.Loaded;

        Assert.NotEmpty(icons.Keys);
        Assert.All(icons.Keys, key =>
        {
            var geometry = Assert.IsAssignableFrom<Geometry>(icons[key]);
            Assert.False(geometry.Bounds.Width <= 0 && geometry.Bounds.Height <= 0, $"{key} is empty");
            Assert.InRange(geometry.Bounds.Right, 0, 24.5);
            Assert.InRange(geometry.Bounds.Bottom, 0, 24.5);
        });
    }

    [AvaloniaFact]
    public void LucideIcon_ResolvesKind_FromAppResources()
    {
        var icon = new LucideIcon { Kind = "Star" };
        var window = new Window { Content = icon };
        window.Show();
        UiHelpers.Settle();

        Assert.True(icon.TryFindResource("Icon.Star", out var resource));
        Assert.IsAssignableFrom<Geometry>(resource);
    }

    [AvaloniaTheory]
    [InlineData(ThemePreference.Dark, "Dark")]
    [InlineData(ThemePreference.Light, "Light")]
    [InlineData(ThemePreference.System, "Default")]
    public void ThemeService_AppliesAndPersistsPreference(ThemePreference preference, string expectedVariant)
    {
        var settings = new InMemorySettingsStore(new AppSettings
        {
            Theme = preference == ThemePreference.System ? ThemePreference.Dark : ThemePreference.System,
        });
        var theme = new ThemeService(settings);

        theme.SetPreference(preference);

        Assert.Equal(expectedVariant, Application.Current!.RequestedThemeVariant?.Key.ToString());
        Assert.Equal(preference, settings.Current.Theme);
        Application.Current.RequestedThemeVariant = ThemeVariant.Default;
    }

    [AvaloniaTheory]
    [InlineData(HostPlatform.MacOS, true, "macos")]
    [InlineData(HostPlatform.Linux, false, null)]
    public void WindowChrome_AppliesPerPlatformStyling(HostPlatform platform, bool extendsClientArea, string? expectedClass)
    {
        var window = new Window();

        new WindowChrome(platform).Apply(window);

        Assert.Equal(extendsClientArea, window.ExtendClientAreaToDecorationsHint);
        if (extendsClientArea)
        {
            Assert.Equal(WindowChrome.HeaderHeight, window.ExtendClientAreaTitleBarHeightHint);
        }
        var styleClasses = window.Classes.Where(c => !c.StartsWith(':')).ToList(); // skip pseudo-classes
        if (expectedClass is null)
        {
            Assert.Empty(styleClasses);
        }
        else
        {
            Assert.Contains(expectedClass, styleClasses);
        }
    }
}

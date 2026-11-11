using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared;
using DevToolBox.App.Tests.TestDoubles;

namespace DevToolBox.App.Tests.Localization;

[Collection(LanguageSwitchCollection.Name)]
public class LocalizerTests
{
    [AvaloniaFact]
    public void TrBinding_UpdatesLive_AndSurvivesGarbageCollection()
    {
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new TrExtension("Home_Title").ProvideValue(null!));
        var window = new Window { Content = text };
        window.Show();
        UiHelpers.Settle();
        Assert.Equal("All tools", text.Text);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        using (new LanguageScope(AppLanguage.Vietnamese))
        {
            UiHelpers.Settle();
            Assert.Equal("Tất cả công cụ", text.Text);
        }

        UiHelpers.Settle();
        Assert.Equal("All tools", text.Text);
    }

    [AvaloniaFact]
    public void ComboBoxChoice_RelabelsSelectionLive()
    {
        var choice = new Choice<int>(() => Strings.Home_Title, 1);
        var combo = new ComboBox { ItemsSource = new[] { choice }, SelectedIndex = 0 };
        var window = new Window { Content = combo };
        window.Show();
        UiHelpers.Settle();
        Assert.Contains(combo.Descendants<TextBlock>(), t => t.Text == "All tools");

        using (new LanguageScope(AppLanguage.Vietnamese))
        {
            UiHelpers.Settle();
            Assert.Contains(combo.Descendants<TextBlock>(), t => t.Text == "Tất cả công cụ");
        }
    }

    [Fact]
    public void FirstLaunch_FollowsOsLanguage()
    {
        Assert.Equal(AppLanguage.Vietnamese, AppLanguages.FromCulture(new System.Globalization.CultureInfo("vi-VN")));
        Assert.Equal(AppLanguage.English, AppLanguages.FromCulture(new System.Globalization.CultureInfo("fr-FR")));
    }
}

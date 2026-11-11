using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;
using DevToolBox.App.Tools.EncodeDecode;
using DevToolBox.App.Tools.Password;

namespace DevToolBox.App.Tests.Localization;

/// <summary>The header switcher relabels the running app in place and remembers the choice.</summary>
[Collection(LanguageSwitchCollection.Name)]
public sealed class LanguageSwitchTests : IDisposable
{
    private readonly TestServices _services = new();

    public void Dispose()
    {
        Localizer.SetLanguage(AppLanguage.English);
        _services.Dispose();
    }

    private (MainWindow Window, ShellViewModel Shell) Show()
    {
        var shell = _services.Get<ShellViewModel>();
        var window = new MainWindow { DataContext = shell };
        window.Show();
        UiHelpers.Settle();
        return (window, shell);
    }

    [AvaloniaFact]
    public void ClickingVietnameseFlag_RelabelsShellAndOpenTool_AndSavesChoice()
    {
        var (window, shell) = Show();
        shell.OpenTool("encode-decode");
        UiHelpers.Settle();
        Assert.True(window.FindControl<RadioButton>("EnglishButton")!.IsChecked);

        window.Click(window.FindControl<RadioButton>("VietnameseButton")!);

        Assert.Equal(AppLanguage.Vietnamese, _services.Settings.Current.Language);
        Assert.True(window.FindControl<RadioButton>("VietnameseButton")!.IsChecked);
        Assert.False(window.FindControl<RadioButton>("EnglishButton")!.IsChecked);
        Assert.Equal("Mã hóa / giải mã Base64", window.FindControl<TextBlock>("PageTitle")!.Text);
        Assert.Equal("Mã hóa", window.Named<RadioButton>("EncodeMode").Content);
        Assert.Contains(shell.SidebarItems, i => i.IsHeader && i.HeaderText == "CHUYỂN ĐỔI");

        shell.GoHome();
        UiHelpers.Settle();
        Assert.Equal("Tất cả công cụ", window.Named<TextBlock>("HomeTitle").Text);

        window.Click(window.FindControl<RadioButton>("EnglishButton")!);
        Assert.Equal("All tools", window.Named<TextBlock>("HomeTitle").Text);
        Assert.Equal(AppLanguage.English, _services.Settings.Current.Language);
    }

    [AvaloniaFact]
    public void SavedChoice_WinsOverOsLanguage()
    {
        var settings = new InMemorySettingsStore(new AppSettings { Language = AppLanguage.Vietnamese });

        new LanguageService(settings).ApplySavedPreference();

        Assert.Equal(AppLanguage.Vietnamese, Localizer.Language);
        Assert.Equal("Tất cả công cụ", Strings.Home_Title);
    }

    [AvaloniaFact]
    public void Search_MatchesCurrentLanguageNames_IgnoringAccents()
    {
        Localizer.SetLanguage(AppLanguage.Vietnamese);
        var jwt = ToolCatalog.All.Single(t => t.Id == "jwt-decoder");

        Assert.True(ToolSearch.Matches(jwt, "giai ma"));
        Assert.True(ToolSearch.Matches(jwt, "Giải mã"));
        Assert.True(ToolSearch.Matches(jwt, "token"));
    }

    [AvaloniaFact]
    public void ToolViewModel_RebuildsStatusAndCoreErrors_InNewLanguage()
    {
        using var vm = new EncodeDecodeViewModel(TimeSpan.Zero) { Mode = CodecMode.Decode, SelectedEncoding = null! };
        vm.SelectedEncoding = vm.Encodings.Single(e => e.Value == Core.Tools.EncodeDecode.TextEncoding.Hex);
        vm.Input = "abc";
        vm.Refresh();
        Assert.StartsWith("Hex input must", vm.ErrorMessage, StringComparison.Ordinal);

        Localizer.SetLanguage(AppLanguage.Vietnamese);

        Assert.StartsWith("Dữ liệu hex phải", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("giải mã", vm.DirectionText);
        Assert.Equal("Nhị phân", vm.Encodings.Single(e => e.Value == Core.Tools.EncodeDecode.TextEncoding.Binary).Label);
    }

    [AvaloniaFact]
    public void PasswordGenerator_KeepsThePassword_WhenLanguageChanges()
    {
        var vm = new PasswordViewModel(new ManualTimeProvider());
        var password = vm.Current;

        Localizer.SetLanguage(AppLanguage.Vietnamese);

        Assert.Equal(password, vm.Current);
        Assert.Contains("bit entropy", vm.EntropyText, StringComparison.Ordinal);
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevToolBox.App.Shell;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools;

namespace DevToolBox.App.Tests.Shell;

/// <summary>Smoke tests over the real tool catalog: every shipped tool must open from Home without errors.</summary>
public class ShippedToolsTests
{
    public static TheoryData<string> ToolIds => [.. ToolCatalog.All.Select(t => t.Id)];

    [Fact]
    public void Catalog_ShipsAllTools() =>
        Assert.Equal(
            ["timestamp", "jwt-decoder", "image-base64", "encode-decode", "html-encode", "url-encode", "json-formatter",
             "regex-tester", "password-generator", "uuid-generator", "hash-generator", "barcode-generator", "app-icons"],
            ToolCatalog.All.Select(t => t.Id));

    [AvaloniaTheory]
    [MemberData(nameof(ToolIds))]
    public void EveryTool_OpensFromHome(string toolId)
    {
        using var services = new TestServices();
        var window = new MainWindow { DataContext = services.Get<ShellViewModel>() };
        window.Show();
        UiHelpers.Settle();

        var tool = ToolCatalog.All.Single(t => t.Id == toolId);
        window.Click(window.CardNamed(tool.Name));

        Assert.Equal(tool.Name, window.FindControl<TextBlock>("PageTitle")!.Text);
        var view = window.FindControl<ContentControl>("PageHost")!.Presenter?.Child;
        Assert.NotNull(view);
        Assert.IsAssignableFrom<ToolViewModelBase>(view.DataContext);
        window.Close();
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using DevToolBox.App.Shared.Controls;
using DevToolBox.App.Tests.TestDoubles;
using DevToolBox.App.Tools.EncodeDecode;
using DevToolBox.App.Tools.Json;
using DevToolBox.App.Tools.Regex;

namespace DevToolBox.App.Tests.Tools;

/// <summary>Headless tests that drive real tool views with keyboard and pointer input.</summary>
public class ToolViewInteractionTests
{
    private static Window Show(Control view)
    {
        var window = new Window { Content = view, Width = 1000, Height = 700 };
        window.Show();
        UiHelpers.Settle();
        return window;
    }

    private static void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
            UiHelpers.Settle();
        }
    }

    [AvaloniaFact]
    public void EncodeDecode_TypingShowsEncodedOutput()
    {
        var vm = new EncodeDecodeViewModel(TimeSpan.FromMilliseconds(1));
        var view = new EncodeDecodeView { DataContext = vm };
        var window = Show(view);

        window.TypeInto(view.FindControl<TextBox>("InputBox")!, "Hello");
        WaitFor(() => vm.Output.Length > 0);

        Assert.Equal("SGVsbG8=", view.FindControl<TextBox>("OutputBox")!.Text);
        Assert.Equal("Auto: encoding", view.FindControl<TextBlock>("DirectionLabel")!.Text);
    }

    [AvaloniaFact]
    public void CodeEditor_SyncsTextBothWays_AndGoesToPosition()
    {
        var editor = new CodeEditor { Language = CodeLanguage.Json, Height = 300 };
        Show(editor);

        editor.Text = "{\n  \"a\": 1\n}";
        Assert.Equal("{\n  \"a\": 1\n}", editor.Editor.Text);

        editor.Editor.Document.Insert(0, "[");
        Assert.Equal("[{\n  \"a\": 1\n}", editor.Text);

        editor.GoTo(2, 5);
        Assert.Equal(2, editor.Editor.TextArea.Caret.Line);
        Assert.Equal(5, editor.Editor.TextArea.Caret.Column);
    }

    [AvaloniaFact]
    public void JsonView_FormatsTypedJson_AndGoToErrorMovesCaret()
    {
        var vm = new JsonViewModel(TimeSpan.FromMilliseconds(1));
        var view = new JsonView { DataContext = vm };
        var window = Show(view);
        var input = view.FindControl<CodeEditor>("InputEditor")!;

        input.Editor.Document.Text = "{\"ok\":true}";
        WaitFor(() => vm.Status is not null);
        Assert.Equal("{\n  \"ok\": true\n}", view.FindControl<CodeEditor>("OutputEditor")!.Editor.Text);

        input.Editor.Document.Text = "{\n  \"a\": 1,\n}";
        WaitFor(() => vm.ErrorMessage is not null);
        window.Click(view.FindControl<CommandButton>("GoToErrorButton")!);

        Assert.Equal(vm.ErrorLocation!.Line, input.Editor.TextArea.Caret.Line);
    }

    [AvaloniaFact]
    public void RegexView_HighlightsMatchesInTheEditor()
    {
        var vm = new RegexViewModel(TimeSpan.FromMilliseconds(1));
        var view = new RegexView { DataContext = vm };
        var window = Show(view);

        window.TypeInto(view.FindControl<TextBox>("PatternBox")!, @"\d+");
        view.FindControl<CodeEditor>("TestEditor")!.Editor.Document.Text = "a1 b22 c333";
        WaitFor(() => vm.Summary == "3 matches");

        Assert.Equal([(1, 1), (4, 2), (8, 3)], view.FindControl<CodeEditor>("TestEditor")!.Highlights!.Select(h => (h.Start, h.Length)));
        Assert.Equal(3, view.FindControl<ListBox>("MatchList")!.ItemCount);
    }

    [AvaloniaFact]
    public async Task CopyButton_PutsTextOnClipboard_AndShowsCopied()
    {
        var button = new CopyButton { Text = "copied value" };
        var window = Show(button);

        window.Click(button);
        var clipboardText = await window.Clipboard!.TryGetTextAsync();

        Assert.Equal("copied value", clipboardText);
        Assert.Equal("Copy", Avalonia.Automation.AutomationProperties.GetName(button));
    }

    [AvaloniaFact]
    public void CopyButton_IsDisabledWithoutText()
    {
        var button = new CopyButton { Text = "" };
        Show(button);

        Assert.False(button.IsEnabled);
    }

    [AvaloniaFact]
    public void ClearButton_ClearsBoundText()
    {
        var vm = new EncodeDecodeViewModel(TimeSpan.FromHours(1)) { Input = "something" };
        var view = new EncodeDecodeView { DataContext = vm };
        var window = Show(view);

        window.Click(view.Descendants<ClearButton>().Single());

        Assert.Equal(string.Empty, vm.Input);
    }
}

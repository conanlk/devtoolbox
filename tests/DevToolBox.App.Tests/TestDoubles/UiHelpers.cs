using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DevToolBox.App.Tests.TestDoubles;

internal static class UiHelpers
{
    /// <summary>Runs queued dispatcher work and a render tick so bindings and layout settle.</summary>
    public static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Real pointer click in the middle of <paramref name="control"/>.</summary>
    public static void Click(this Window window, Control control)
    {
        control.BringIntoView();
        Settle();
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
                     ?? throw new InvalidOperationException("Control is not in the window.");
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Settle();
    }

    public static void TypeInto(this Window window, TextBox textBox, string text)
    {
        textBox.Focus();
        window.KeyTextInput(text);
        Settle();
    }

    public static IEnumerable<T> Descendants<T>(this Visual root) where T : Visual =>
        root.GetVisualDescendants().OfType<T>();

    /// <summary>Finds a named control anywhere in the visual tree, across UserControl name scopes.</summary>
    public static T Named<T>(this Visual root, string name) where T : Control =>
        root.Descendants<T>().Single(c => c.Name == name);

    public static Button CardNamed(this Window window, string name) =>
        window.Descendants<Button>().Single(b => b.Classes.Contains("card") && AutomationName(b) == name);

    public static string? AutomationName(Control control) =>
        Avalonia.Automation.AutomationProperties.GetName(control);
}

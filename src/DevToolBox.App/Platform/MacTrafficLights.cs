using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DevToolBox.App.Platform;

/// <summary>
/// Centres the macOS window buttons (close / minimise / zoom) vertically in a custom title bar of the given
/// height, like Electron's <c>trafficLightPosition</c>. Avalonia always places them in the default ~28 pt bar,
/// which looks misaligned in our 48 pt header. macOS re-lays them out on resize, so call this again then.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class MacTrafficLights
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    /// <summary>Gap between the left window edge and the close button, in points (macOS default ≈ 7–9).</summary>
    private const double LeftInset = 16;

    /// <summary>Distance between button origins (macOS default spacing).</summary>
    private const double Spacing = 20;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CGPoint(double X, double Y);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CGSize(double Width, double Height);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CGRect(CGPoint Origin, CGSize Size);

    /// <param name="nsWindow">The NSWindow* (from <c>IMacOSTopLevelPlatformHandle.NSWindow</c>).</param>
    public static void Center(IntPtr nsWindow, double titleBarHeight)
    {
        if (nsWindow == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var close = Button(nsWindow, 0);
            if (close == IntPtr.Zero)
            {
                return;
            }

            // The buttons live in NSTitlebarView inside NSTitlebarContainerView; grow the container to our
            // header height (anchored to the top edge; Cocoa's origin is bottom-left).
            var container = Send(Send(close, Sel("superview")), Sel("superview"));
            var window = Frame(nsWindow);
            if (container != IntPtr.Zero)
            {
                SendRect(container, Sel("setFrame:"),
                    new CGRect(new CGPoint(0, window.Size.Height - titleBarHeight), new CGSize(window.Size.Width, titleBarHeight)));
            }

            for (var i = 0; i < 3; i++)
            {
                var button = Button(nsWindow, i);
                if (button == IntPtr.Zero)
                {
                    continue;
                }

                var size = Frame(button).Size;
                SendPoint(button, Sel("setFrameOrigin:"),
                    new CGPoint(LeftInset + (i * Spacing), Math.Round((titleBarHeight - size.Height) / 2)));
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Trace.TraceWarning($"Could not position the window buttons: {ex.Message}");
        }
    }

    /// <summary>Button frames as "x,y,wxh;…" (Cocoa coordinates, origin bottom-left) for the smoke-test output.</summary>
    public static string Describe(IntPtr nsWindow)
    {
        static string Format(CGRect f) =>
            FormattableString.Invariant($"{f.Origin.X:F0},{f.Origin.Y:F0},{f.Size.Width:F0}x{f.Size.Height:F0}");

        var buttons = Enumerable.Range(0, 3).Select(i => Button(nsWindow, i)).Where(b => b != IntPtr.Zero).ToList();
        if (buttons.Count == 0)
        {
            return "none";
        }

        var titleBar = Send(buttons[0], Sel("superview"));
        var container = Send(titleBar, Sel("superview"));
        return $"{string.Join(';', buttons.Select(b => Format(Frame(b))))} titlebar={Format(Frame(titleBar))} container={Format(Frame(container))} window={Format(Frame(nsWindow))}";
    }

    private static IntPtr Button(IntPtr nsWindow, int kind) => SendLong(nsWindow, Sel("standardWindowButton:"), kind);

    private static IntPtr Sel(string name) => sel_registerName(name);

    // x86_64 returns structs larger than 16 bytes through objc_msgSend_stret; arm64 returns them in registers.
    private static CGRect Frame(IntPtr view)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            SendStret(out var rect, view, Sel("frame"));
            return rect;
        }

        return SendRectReturn(view, Sel("frame"));
    }

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendLong(IntPtr receiver, IntPtr selector, nint argument);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendRect(IntPtr receiver, IntPtr selector, CGRect rect);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendPoint(IntPtr receiver, IntPtr selector, CGPoint point);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial CGRect SendRectReturn(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend_stret")]
    private static partial void SendStret(out CGRect result, IntPtr receiver, IntPtr selector);
}

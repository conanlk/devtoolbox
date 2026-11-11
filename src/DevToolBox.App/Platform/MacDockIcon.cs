using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DevToolBox.App.Platform;

/// <summary>
/// Sets the macOS Dock icon at runtime and shows/hides the app in the Dock. Setting the icon is needed when the app runs as a bare executable (`dotnet run`, the CI
/// publish folder): without an .app bundle macOS shows the generic "exec" icon. Bundled builds get the same
/// image from DevToolBox.icns, so calling this there is harmless.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class MacDockIcon
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const nint NSApplicationActivationPolicyRegular = 0;
    private const nint NSApplicationActivationPolicyAccessory = 1;

    /// <summary>Sets the Dock icon from PNG bytes. Must be called on the main thread after Avalonia starts.</summary>
    public static void Set(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        var buffer = Marshal.AllocHGlobal(png.Length);
        try
        {
            Marshal.Copy(png, 0, buffer, png.Length);

            // NSData *data = [NSData dataWithBytes:buffer length:png.Length];  (copies the bytes)
            var data = SendPtrLength(GetClass("NSData"), Selector("dataWithBytes:length:"), buffer, (nuint)png.Length);

            // NSImage *image = [[NSImage alloc] initWithData:data];
            var image = SendPtr(Send(GetClass("NSImage"), Selector("alloc")), Selector("initWithData:"), data);
            if (image == IntPtr.Zero)
            {
                return;
            }

            // [[NSApplication sharedApplication] setApplicationIconImage:image];
            SendPtr(SharedApplication(), Selector("setApplicationIconImage:"), image);
            Send(image, Selector("release"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Not fatal: the Dock just keeps the default icon.
            Trace.TraceWarning($"Could not set the Dock icon: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Regular activation policy: Dock icon and app menu. Brings the app to the front.</summary>
    public static void ShowInDock()
    {
        if (SetActivationPolicy(NSApplicationActivationPolicyRegular))
        {
            SendBool(SharedApplication(), Selector("activateIgnoringOtherApps:"), 1);
        }
    }

    /// <summary>Accessory activation policy: menu bar extra only, no Dock icon.</summary>
    public static void HideFromDock() => SetActivationPolicy(NSApplicationActivationPolicyAccessory);

    private static bool SetActivationPolicy(nint policy)
    {
        try
        {
            // [[NSApplication sharedApplication] setActivationPolicy:policy];
            return SendNInt(SharedApplication(), Selector("setActivationPolicy:"), policy) != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Trace.TraceWarning($"Could not change the Dock presence: {ex.Message}");
            return false;
        }
    }

    private static IntPtr SharedApplication() => Send(GetClass("NSApplication"), Selector("sharedApplication"));

    private static IntPtr GetClass(string name) => objc_getClass(name);

    private static IntPtr Selector(string name) => sel_registerName(name);

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtrLength(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial byte SendNInt(IntPtr receiver, IntPtr selector, nint argument);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendBool(IntPtr receiver, IntPtr selector, byte argument);
}

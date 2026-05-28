using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryn.Core.Internal;

[SupportedOSPlatform("macos")]
internal static partial class MacOsTitleBar
{
    internal static unsafe void ApplyTransparentTitleBar(nint nsWindowPtr)
    {
        if (nsWindowPtr == 0) return;

        var nsWindow = (void*)nsWindowPtr;

        // window.titleVisibility = NSWindowTitleHidden (1)
        objc_msgSend_nint(nsWindow, sel_registerName("setTitleVisibility:"), 1);

        // window.titlebarAppearsTransparent = YES
        objc_msgSend_bool(nsWindow, sel_registerName("setTitlebarAppearsTransparent:"), 1);
    }

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_bool(void* receiver, nint selector, byte value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_nint(void* receiver, nint selector, nint value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ret_nint(void* receiver, nint selector);
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Ryn.Plugins.Tray.Backends;

[SupportedOSPlatform("macos")]
internal sealed partial class MacOsTrayBackend : ITrayBackend
{
    private nint _statusItem;
    private nint _menu;
    private nint _handlerObject;
    private bool _disposed;

    private readonly List<TrayMenuItem> _menuItems = [];
    private readonly object _lock = new();

    private static MacOsTrayBackend? _instance;
    private static nint _handlerClass;
    private static bool _classRegistered;

    public event Action? IconClicked;
    public event Action<string>? MenuItemClicked;

    public unsafe void Show(string? iconPath, string tooltip)
    {
        if (_statusItem != 0) return;

        _instance = this;
        EnsureHandlerClass();

        var pool = objc_autoreleasePoolPush();
        try
        {
            var handlerAlloc = objc_msgSend_ret_nint((void*)_handlerClass, sel_registerName("alloc"));
            _handlerObject = objc_msgSend_ret_nint((void*)handlerAlloc, sel_registerName("init"));

            var statusBar = (void*)objc_msgSend_ret_nint(
                (void*)objc_getClass("NSStatusBar"),
                sel_registerName("systemStatusBar"));

            _statusItem = objc_msgSend_double_ret_nint(
                statusBar, sel_registerName("statusItemWithLength:"), -1.0);

            var button = (void*)objc_msgSend_ret_nint(
                (void*)_statusItem, sel_registerName("button"));

            if (iconPath is not null && File.Exists(iconPath))
            {
                var pathStr = CreateNSString(iconPath);
                var imgAlloc = objc_msgSend_ret_nint(
                    (void*)objc_getClass("NSImage"), sel_registerName("alloc"));
                var image = objc_msgSend_ptr_ret_nint(
                    (void*)imgAlloc, sel_registerName("initWithContentsOfFile:"), (void*)pathStr);
                if (image != 0)
                {
                    objc_msgSend_bool((void*)image, sel_registerName("setTemplate:"), 1);
                    objc_msgSend_ptr(button, sel_registerName("setImage:"), (void*)image);
                }
            }
            else
            {
                var title = CreateNSString("●");
                objc_msgSend_ptr(button, sel_registerName("setTitle:"), (void*)title);
            }

            var tooltipStr = CreateNSString(tooltip);
            objc_msgSend_ptr(button, sel_registerName("setToolTip:"), (void*)tooltipStr);

            objc_msgSend_ptr(button, sel_registerName("setTarget:"), (void*)_handlerObject);
            objc_msgSend_nint(button, sel_registerName("setAction:"),
                sel_registerName("statusItemClicked:"));
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public unsafe void Hide()
    {
        if (_statusItem == 0) return;

        var statusBar = (void*)objc_msgSend_ret_nint(
            (void*)objc_getClass("NSStatusBar"),
            sel_registerName("systemStatusBar"));
        objc_msgSend_ptr(statusBar, sel_registerName("removeStatusItem:"), (void*)_statusItem);
        _statusItem = 0;
    }

    public unsafe void SetTooltip(string tooltip)
    {
        if (_statusItem == 0) return;

        var pool = objc_autoreleasePoolPush();
        try
        {
            var button = (void*)objc_msgSend_ret_nint(
                (void*)_statusItem, sel_registerName("button"));
            var tooltipStr = CreateNSString(tooltip);
            objc_msgSend_ptr(button, sel_registerName("setToolTip:"), (void*)tooltipStr);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public void SetMenu(IReadOnlyList<TrayMenuItem> items)
    {
        lock (_lock)
        {
            _menuItems.Clear();
            _menuItems.AddRange(items);
        }
        RebuildMenu();
    }

    public void ShowNotification(string title, string message)
    {
        var escapedTitle = title
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        var escapedMsg = message
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        var script = $"display notification \"{escapedMsg}\" with title \"{escapedTitle}\"";
        try
        {
            var psi = new ProcessStartInfo("osascript")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add(script);
            Process.Start(psi)?.WaitForExit();
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private unsafe void RebuildMenu()
    {
        if (_statusItem == 0) return;

        var pool = objc_autoreleasePoolPush();
        try
        {
            if (_menu != 0)
                objc_msgSend_ret_nint((void*)_menu, sel_registerName("release"));

            var menuAlloc = objc_msgSend_ret_nint(
                (void*)objc_getClass("NSMenu"), sel_registerName("alloc"));
            _menu = objc_msgSend_ret_nint((void*)menuAlloc, sel_registerName("init"));

            objc_msgSend_bool((void*)_menu, sel_registerName("setAutoenablesItems:"), 0);

            lock (_lock)
            {
                for (var i = 0; i < _menuItems.Count; i++)
                {
                    var item = _menuItems[i];

                    if (item.Separator)
                    {
                        var separator = (void*)objc_msgSend_ret_nint(
                            (void*)objc_getClass("NSMenuItem"),
                            sel_registerName("separatorItem"));
                        objc_msgSend_ptr((void*)_menu, sel_registerName("addItem:"), separator);
                    }
                    else
                    {
                        var titleStr = CreateNSString(item.Label);
                        var keyStr = CreateNSString("");
                        var action = sel_registerName("menuItemClicked:");

                        var miAlloc = objc_msgSend_ret_nint(
                            (void*)objc_getClass("NSMenuItem"), sel_registerName("alloc"));
                        var menuItem = (void*)objc_msgSend_3nint_ret_nint(
                            (void*)miAlloc,
                            sel_registerName("initWithTitle:action:keyEquivalent:"),
                            titleStr, action, keyStr);

                        objc_msgSend_ptr(menuItem, sel_registerName("setTarget:"),
                            (void*)_handlerObject);
                        objc_msgSend_nint(menuItem, sel_registerName("setTag:"), i);

                        if (!item.Enabled)
                            objc_msgSend_bool(menuItem, sel_registerName("setEnabled:"), 0);

                        objc_msgSend_ptr((void*)_menu, sel_registerName("addItem:"), menuItem);
                    }
                }
            }

            objc_msgSend_ptr((void*)_menu, sel_registerName("setDelegate:"),
                (void*)_handlerObject);
            objc_msgSend_ptr((void*)_statusItem, sel_registerName("setMenu:"), (void*)_menu);
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    private static unsafe void EnsureHandlerClass()
    {
        if (_classRegistered) return;

        var superclass = objc_getClass("NSObject");
        _handlerClass = objc_allocateClassPair(superclass, "RynTrayHandler", 0);

        class_addMethod(
            _handlerClass,
            sel_registerName("statusItemClicked:"),
            (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&OnStatusItemClicked,
            "v@:@");

        class_addMethod(
            _handlerClass,
            sel_registerName("menuItemClicked:"),
            (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&OnMenuItemClicked,
            "v@:@");

        class_addMethod(
            _handlerClass,
            sel_registerName("menuWillOpen:"),
            (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&OnMenuWillOpen,
            "v@:@");

        objc_registerClassPair(_handlerClass);
        _classRegistered = true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnStatusItemClicked(nint self, nint sel, nint sender)
    {
        _instance?.IconClicked?.Invoke();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe void OnMenuItemClicked(nint self, nint sel, nint sender)
    {
        var tag = (int)objc_msgSend_ret_nint((void*)sender, sel_registerName("tag"));
        var instance = _instance;
        if (instance is null) return;

        string? itemId;
        lock (instance._lock)
        {
            if (tag >= 0 && tag < instance._menuItems.Count)
                itemId = instance._menuItems[tag].Id;
            else
                return;
        }
        instance.MenuItemClicked?.Invoke(itemId);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnMenuWillOpen(nint self, nint sel, nint menu)
    {
        _instance?.IconClicked?.Invoke();
    }

    private static unsafe nint CreateNSString(string str)
    {
        var utf8 = Encoding.UTF8.GetBytes(str + "\0");
        fixed (byte* ptr = utf8)
        {
            return objc_msgSend_ptr_ret_nint(
                (void*)objc_getClass("NSString"),
                sel_registerName("stringWithUTF8String:"),
                ptr);
        }
    }

    ~MacOsTrayBackend() => Dispose();

    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);

        Hide();

        if (_menu != 0)
        {
            objc_msgSend_ret_nint((void*)_menu, sel_registerName("release"));
            _menu = 0;
        }

        if (_handlerObject != 0)
        {
            objc_msgSend_ret_nint((void*)_handlerObject, sel_registerName("release"));
            _handlerObject = 0;
        }

        if (_instance == this)
            _instance = null;
    }

    // --- ObjC Runtime P/Invoke ---

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint sel_registerName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint objc_getClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint objc_allocateClassPair(
        nint superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void objc_registerClassPair(nint cls);

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool class_addMethod(
        nint cls, nint sel, nint imp, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint objc_autoreleasePoolPush();

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_bool(void* receiver, nint selector, byte value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_nint(void* receiver, nint selector, nint value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_ptr(
        void* receiver, nint selector, void* value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ret_nint(void* receiver, nint selector);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ptr_ret_nint(
        void* receiver, nint selector, void* value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_double_ret_nint(
        void* receiver, nint selector, double value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_3nint_ret_nint(
        void* receiver, nint selector, nint arg1, nint arg2, nint arg3);
}

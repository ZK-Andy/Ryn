using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Ryn.Plugins.Audio.Backends;

[SupportedOSPlatform("macos")]
internal sealed partial class MacOsAudioBackend : IAudioBackend
{
    private nint _currentSound;
    private bool _disposed;
    private readonly object _lock = new();

    public unsafe void Play(string path, int volume, bool loop)
    {
        Stop();

        var pool = objc_autoreleasePoolPush();
        try
        {
            var pathStr = CreateNSString(path);

            var soundAlloc = objc_msgSend_ret_nint(
                (void*)objc_getClass("NSSound"), sel_registerName("alloc"));
            var sound = objc_msgSend_ptr_bool_ret_nint(
                (void*)soundAlloc, sel_registerName("initWithContentsOfFile:byReference:"), (void*)pathStr, 1);

            if (sound != 0)
            {
                objc_msgSend_float((void*)sound, sel_registerName("setVolume:"), volume / 100.0f);
                objc_msgSend_bool((void*)sound, sel_registerName("setLoops:"), loop ? (byte)1 : (byte)0);
                lock (_lock)
                {
                    _currentSound = sound;
                }
                objc_msgSend_ret_bool((void*)sound, sel_registerName("play"));
            }
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public unsafe void PlaySystem(string name)
    {
        var path = $"/System/Library/Sounds/{name}.aiff";
        if (File.Exists(path))
        {
            Play(path, 100, false);
        }
    }

    public unsafe void Stop()
    {
        lock (_lock)
        {
            if (_currentSound != 0)
            {
                objc_msgSend_void((void*)_currentSound, sel_registerName("stop"));
                objc_msgSend_ret_nint((void*)_currentSound, sel_registerName("release"));
                _currentSound = 0;
            }
        }
    }

    public unsafe void SetVolume(int percent)
    {
        lock (_lock)
        {
            if (_currentSound != 0)
            {
                var volume = percent / 100.0f;
                objc_msgSend_float((void*)_currentSound, sel_registerName("setVolume:"), volume);
            }
        }
    }

    public unsafe bool IsPlaying()
    {
        lock (_lock)
        {
            if (_currentSound == 0) return false;
            return objc_msgSend_ret_bool((void*)_currentSound, sel_registerName("isPlaying"));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
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
    private static partial nint objc_autoreleasePoolPush();

    [LibraryImport("libobjc.dylib")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ret_nint(void* receiver, nint selector);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ptr_ret_nint(
        void* receiver, nint selector, void* value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial nint objc_msgSend_ptr_bool_ret_nint(
        void* receiver, nint selector, void* value, byte boolValue);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_void(void* receiver, nint selector);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_bool(
        void* receiver, nint selector, byte value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial void objc_msgSend_float(
        void* receiver, nint selector, float value);

    [LibraryImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool objc_msgSend_ret_bool(void* receiver, nint selector);
}

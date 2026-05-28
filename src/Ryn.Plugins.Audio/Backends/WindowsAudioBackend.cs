using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryn.Plugins.Audio.Backends;

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsAudioBackend : IAudioBackend
{
    private const uint SndFilename = 0x20000;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndAlias = 0x10000;
    private const uint SndLoop = 0x0008;

    private volatile bool _playing;
    private bool _disposed;

    public void Play(string path, int volume, bool loop)
    {
        Stop();
        SetVolume(volume);
        var flags = SndFilename | SndAsync | SndNoDefault;
        if (loop) flags |= SndLoop;
        _playing = PlaySound(path, 0, flags);
    }

    public void PlaySystem(string name)
    {
        Stop();
        _playing = PlaySound(name, 0, SndAlias | SndAsync | SndNoDefault);
    }

    public void Stop()
    {
        PlaySound(null, 0, 0);
        _playing = false;
    }

    public void SetVolume(int percent)
    {
        // Map 0-100 to 0x0000-0xFFFF for both left and right channels
        var scaled = (uint)(percent * 0xFFFF / 100);
        var dwVolume = scaled | (scaled << 16);
        _ = waveOutSetVolume(0, dwVolume);
    }

    public bool IsPlaying() => _playing;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // --- winmm.dll P/Invoke ---

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(string? pszSound, nint hmod, uint fdwSound);

    [LibraryImport("winmm.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint waveOutSetVolume(nint hwo, uint dwVolume);
}

using System.Diagnostics;
using System.Runtime.Versioning;

namespace Ryn.Plugins.Audio.Backends;

[SupportedOSPlatform("linux")]
internal sealed class LinuxAudioBackend : IAudioBackend
{
    private Process? _currentProcess;
    private readonly object _lock = new();
    private volatile bool _looping;
    private string? _loopPath;
    private bool _disposed;

    public void Play(string path, int volume, bool loop)
    {
        Stop();

        _looping = loop;
        _loopPath = loop ? path : null;

        if (volume < 100 && IsToolAvailable("pactl"))
        {
            try
            {
                var psi = new ProcessStartInfo("pactl")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("set-sink-volume");
                psi.ArgumentList.Add("@DEFAULT_SINK@");
                psi.ArgumentList.Add($"{volume}%");
                Process.Start(psi)?.WaitForExit(2000);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        StartPlayback(path);
    }

    private void StartPlayback(string path)
    {
        var tool = IsToolAvailable("paplay") ? "paplay" : "aplay";

        var psi = new ProcessStartInfo(tool)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(path);

        try
        {
            var process = Process.Start(psi);
            if (process is not null)
            {
                lock (_lock)
                {
                    _currentProcess = process;
                }

                process.EnableRaisingEvents = true;
                process.Exited += (_, _) =>
                {
                    lock (_lock)
                    {
                        if (_currentProcess == process)
                            _currentProcess = null;
                    }
                    process.Dispose();

                    if (_looping && _loopPath is not null)
                        StartPlayback(_loopPath);
                };
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void PlaySystem(string name)
    {
        Stop();

        // Use canberra-gtk-play for freedesktop sound theme names
        if (!IsToolAvailable("canberra-gtk-play")) return;

        var psi = new ProcessStartInfo("canberra-gtk-play")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(name);

        try
        {
            var process = Process.Start(psi);
            if (process is not null)
            {
                lock (_lock)
                {
                    _currentProcess = process;
                }

                process.EnableRaisingEvents = true;
                process.Exited += (_, _) =>
                {
                    lock (_lock)
                    {
                        if (_currentProcess == process)
                            _currentProcess = null;
                    }
                    process.Dispose();
                };
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Stop()
    {
        _looping = false;
        _loopPath = null;

        Process? process;
        lock (_lock)
        {
            process = _currentProcess;
            _currentProcess = null;
        }

        if (process is not null && !process.HasExited)
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException) { }
            process.Dispose();
        }
    }

    public void SetVolume(int percent)
    {
        // Volume control not easily supported on Linux without PulseAudio API bindings.
        // Return silently as documented.
    }

    public bool IsPlaying()
    {
        lock (_lock)
        {
            return _currentProcess is not null && !_currentProcess.HasExited;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private static bool IsToolAvailable(string tool)
    {
        var psi = new ProcessStartInfo("which")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(tool);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

namespace Ryn.Plugins.Audio.Backends;

internal sealed class StubAudioBackend : IAudioBackend
{
    public void Play(string path, int volume, bool loop) { }
    public void PlaySystem(string name) { }
    public void Stop() { }
    public void SetVolume(int percent) { }
    public bool IsPlaying() => false;
    public void Dispose() { }
}

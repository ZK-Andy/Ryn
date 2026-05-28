namespace Ryn.Plugins.Audio;

internal interface IAudioBackend : IDisposable
{
    public void Play(string path, int volume, bool loop);
    public void PlaySystem(string name);
    public void Stop();
    public void SetVolume(int percent);
    public bool IsPlaying();
}

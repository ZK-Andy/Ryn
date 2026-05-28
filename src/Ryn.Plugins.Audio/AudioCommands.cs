using Ryn.Ipc;

namespace Ryn.Plugins.Audio;

#pragma warning disable CA1812 // Instantiated by generated DI code
internal sealed class AudioCommands
#pragma warning restore CA1812
{
    private readonly AudioService _service;

    public AudioCommands(AudioService service) => _service = service;

    [RynCommand("audio.play")]
    public void Play(string path, int volume, bool loop) => _service.Play(path, volume, loop);

    [RynCommand("audio.playSystem")]
    public void PlaySystem(string name) => _service.PlaySystem(name);

    [RynCommand("audio.stop")]
    public void Stop() => _service.Stop();

    [RynCommand("audio.setVolume")]
    public void SetVolume(int percent) => _service.SetVolume(percent);

    [RynCommand("audio.isPlaying")]
    public bool IsPlaying() => _service.IsPlaying();
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Ryn.Ipc;

namespace Ryn.Plugins.Updater;

[RynJsonContext(typeof(UpdaterJsonContext))]
#pragma warning disable CA1812 // Instantiated by generated DI code
internal sealed class UpdaterCommands
#pragma warning restore CA1812
{
    private readonly UpdaterService _service;

    public UpdaterCommands(UpdaterService service) => _service = service;

    [RynCommand("updater.check")]
    public async ValueTask<string> CheckAsync()
    {
        var update = await _service.CheckForUpdateAsync().ConfigureAwait(false);
        if (update is null)
            return "null";

        return JsonSerializer.Serialize(update, UpdaterJsonContext.Default.UpdateInfo);
    }

    [RynCommand("updater.download")]
    public async ValueTask<string> DownloadAsync()
    {
        var update = await _service.CheckForUpdateAsync().ConfigureAwait(false);
        if (update is null)
            throw new InvalidOperationException("No update available.");

        var path = await _service.DownloadUpdateAsync(update).ConfigureAwait(false);
        return path;
    }

    [RynCommand("updater.apply")]
    public async ValueTask ApplyAsync(string downloadPath)
    {
        await _service.ApplyUpdateAsync(downloadPath).ConfigureAwait(false);
    }
}

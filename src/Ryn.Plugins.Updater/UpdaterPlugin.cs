using Ryn.Core;

namespace Ryn.Plugins.Updater;

#pragma warning disable CA1812 // Instantiated by DI
internal sealed class UpdaterPlugin : IRynPlugin
#pragma warning restore CA1812
{
    private readonly UpdaterOptions _options;
    private readonly UpdaterService _service;

    public UpdaterPlugin(UpdaterOptions options, UpdaterService service)
    {
        _options = options;
        _service = service;
    }

    public string Name => "Updater";

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CheckOnStartup)
            return;

        // Fire-and-forget startup check — log-only, don't block app startup
        try
        {
            var update = await _service.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
            if (update is not null)
            {
                Console.WriteLine($"[Ryn.Updater] Update available: {update.Version}");
                Console.WriteLine($"[Ryn.Updater] Release URL: {update.ReleaseUrl}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Ryn.Updater] Startup update check failed: {ex.Message}");
        }
    }
}

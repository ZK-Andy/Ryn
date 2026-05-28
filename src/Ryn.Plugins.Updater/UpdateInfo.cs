namespace Ryn.Plugins.Updater;

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public required Uri ReleaseUrl { get; init; }
    public required Uri AssetUrl { get; init; }
    public string? ReleaseNotes { get; init; }
}

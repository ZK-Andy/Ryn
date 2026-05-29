namespace Ryn.Plugins.Updater;

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public required Uri ReleaseUrl { get; init; }
    public required Uri AssetUrl { get; init; }

    /// <summary>URL of the detached signature asset (asset name + <c>SignatureAssetSuffix</c>). Required to apply an update.</summary>
    public Uri? SignatureUrl { get; init; }

    public string? ReleaseNotes { get; init; }
}

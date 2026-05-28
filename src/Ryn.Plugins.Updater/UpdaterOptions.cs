namespace Ryn.Plugins.Updater;

public sealed class UpdaterOptions
{
    public string? GitHubOwner { get; set; }
    public string? GitHubRepo { get; set; }
    public string? CurrentVersion { get; set; }
    public string? AssetPattern { get; set; }
    public bool CheckOnStartup { get; set; }
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);
}

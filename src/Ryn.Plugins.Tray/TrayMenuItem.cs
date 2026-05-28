namespace Ryn.Plugins.Tray;

public sealed class TrayMenuItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Separator { get; init; }
}

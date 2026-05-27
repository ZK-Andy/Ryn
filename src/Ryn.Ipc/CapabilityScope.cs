namespace Ryn.Ipc;

public sealed class CapabilityScope
{
    public static CapabilityScope Empty { get; } = new(null, null);

    public IReadOnlyList<string>? AllowedPaths { get; }
    public IReadOnlyList<string>? AllowedCommands { get; }

    public CapabilityScope(IReadOnlyList<string>? allowedPaths, IReadOnlyList<string>? allowedCommands)
    {
        AllowedPaths = allowedPaths;
        AllowedCommands = allowedCommands;
    }

    public bool HasPathPolicy => AllowedPaths is not null;
    public bool HasCommandPolicy => AllowedCommands is not null;
}

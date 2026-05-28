namespace Ryn.Plugins.Shell;

public sealed class ShellOptions
{
    public List<string> AllowedCommands { get; set; } = [];
    public List<string> DenyArgPrefixes { get; set; } = [];
}

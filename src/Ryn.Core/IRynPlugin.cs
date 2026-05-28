namespace Ryn.Core;

/// <summary>Defines a plugin that extends Ryn application functionality.</summary>
public interface IRynPlugin
{
    /// <summary>The display name of this plugin.</summary>
    public string Name { get; }

    /// <summary>Called once before the window opens to initialize the plugin.</summary>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}

namespace Ryn.Core;

/// <summary>Event data for deep link URL activations.</summary>
public sealed class DeepLinkEventArgs : EventArgs
{
    /// <summary>The deep link URL that activated the application.</summary>
    public required Uri Url { get; init; }
}

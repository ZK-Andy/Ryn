namespace Ryn.Core;

/// <summary>Event data for file drop operations. Note: browser security limits this to file names only, not full paths.</summary>
public sealed class FileDropEventArgs : EventArgs
{
    /// <summary>Names of the dropped files.</summary>
    public required IReadOnlyList<string> FileNames { get; init; }

    /// <summary>Drop position X coordinate in the webview.</summary>
    public int X { get; init; }

    /// <summary>Drop position Y coordinate in the webview.</summary>
    public int Y { get; init; }
}

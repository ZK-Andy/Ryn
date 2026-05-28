namespace Ryn.Core;

/// <summary>Provides data for the <see cref="IRynWindow.Closing"/> event, allowing cancellation of the window close.</summary>
public sealed class WindowClosingEventArgs : EventArgs
{
    /// <summary>Gets or sets a value indicating whether the close operation should be cancelled.</summary>
    /// <remarks>Set to <see langword="true"/> to prevent the window from closing (e.g. to show an "unsaved changes" dialog).</remarks>
    public bool Cancel { get; set; }
}

/// <summary>Provides data for the <see cref="IRynWindow.Resized"/> event with the new window dimensions.</summary>
public sealed class WindowResizedEventArgs : EventArgs
{
    /// <summary>The new width of the window in pixels.</summary>
    public int Width { get; init; }

    /// <summary>The new height of the window in pixels.</summary>
    public int Height { get; init; }
}

/// <summary>Provides data for the <see cref="IRynWindow.Moved"/> event with the new window position.</summary>
public sealed class WindowMovedEventArgs : EventArgs
{
    /// <summary>The new X coordinate of the window in screen pixels.</summary>
    public int X { get; init; }

    /// <summary>The new Y coordinate of the window in screen pixels.</summary>
    public int Y { get; init; }
}

/// <summary>Represents the visual state of a window.</summary>
public enum WindowState
{
    /// <summary>The window is in its normal (restored) state.</summary>
    Normal,

    /// <summary>The window is minimized to the taskbar/dock.</summary>
    Minimized,

    /// <summary>The window is maximized to fill the screen.</summary>
    Maximized,
}

/// <summary>Provides data for the <see cref="IRynWindow.StateChanged"/> event with the new window state.</summary>
public sealed class WindowStateChangedEventArgs : EventArgs
{
    /// <summary>The new state of the window.</summary>
    public WindowState State { get; init; }
}

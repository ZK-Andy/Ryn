namespace Ryn.Core;

/// <summary>Represents the native application window.</summary>
public interface IRynWindow
{
    /// <summary>The window title text.</summary>
    public string Title { get; set; }

    /// <summary>The window width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>The window height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Whether the window can be resized by the user.</summary>
    public bool Resizable { get; set; }

    /// <summary>Shows the window if it is hidden.</summary>
    public ValueTask ShowAsync(CancellationToken cancellationToken = default);

    /// <summary>Hides the window without closing it.</summary>
    public ValueTask HideAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the window and exits the event loop.</summary>
    public ValueTask CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Waits until the window is closed by the user or programmatically.</summary>
    public ValueTask WaitForCloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates the webview to the specified URL.</summary>
    public ValueTask NavigateAsync(Uri url, CancellationToken cancellationToken = default);

    /// <summary>Evaluates a JavaScript expression in the webview and returns the result.</summary>
    public ValueTask<string> EvaluateJavaScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>Closes the window synchronously.</summary>
    public void Close();

    /// <summary>Minimizes the window.</summary>
    public void Minimize();

    /// <summary>Toggles between maximized and restored window states.</summary>
    public void ToggleMaximize();

    /// <summary>Initiates a window drag operation (for frameless windows).</summary>
    public void StartDrag();

    /// <summary>Initiates a window resize operation from the specified edge.</summary>
    public void StartResize(WindowEdge edge);
}

/// <summary>Specifies which edge or corner of the window to resize from.</summary>
[Flags]
public enum WindowEdge
{
    /// <summary>Top edge.</summary>
    Top = 1,

    /// <summary>Bottom edge.</summary>
    Bottom = 2,

    /// <summary>Left edge.</summary>
    Left = 4,

    /// <summary>Right edge.</summary>
    Right = 8,

    /// <summary>Top-left corner.</summary>
    TopLeft = Top | Left,

    /// <summary>Top-right corner.</summary>
    TopRight = Top | Right,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft = Bottom | Left,

    /// <summary>Bottom-right corner.</summary>
    BottomRight = Bottom | Right,
}

namespace Ryn.Core;

/// <summary>System color scheme preference.</summary>
public enum AppTheme
{
    /// <summary>Light color scheme.</summary>
    Light,

    /// <summary>Dark color scheme.</summary>
    Dark,
}

/// <summary>Event data for system theme changes.</summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>The new system theme.</summary>
    public required AppTheme Theme { get; init; }
}

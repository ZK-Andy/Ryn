namespace Ryn.Plugins.Tray.Backends;

#pragma warning disable CS0067 // Events unused on stub — required by interface
internal sealed class StubTrayBackend : ITrayBackend
{
    public event Action? IconClicked;
    public event Action<string>? MenuItemClicked;

    public void Show(string? iconPath, string tooltip) { }
    public void Hide() { }
    public void SetTooltip(string tooltip) { }
    public void SetMenu(IReadOnlyList<TrayMenuItem> items) { }
    public void ShowNotification(string title, string message) { }
    public void Dispose() { }
}

namespace Ryn.Plugins.Tray;

internal interface ITrayBackend : IDisposable
{
    public void Show(string? iconPath, string tooltip);
    public void Hide();
    public void SetTooltip(string tooltip);
    public void SetMenu(IReadOnlyList<TrayMenuItem> items);
    public void ShowNotification(string title, string message);

    public event Action? IconClicked;
    public event Action<string>? MenuItemClicked;
}

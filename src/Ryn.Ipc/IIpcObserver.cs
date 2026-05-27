namespace Ryn.Ipc;

public interface IIpcObserver
{
    public void OnCommandStarted(string command);
    public void OnCommandCompleted(string command, long elapsedMs);
    public void OnCommandFailed(string command, long elapsedMs, Exception exception);
    public void OnCommandDenied(string command);
}

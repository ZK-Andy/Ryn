namespace Ryn.Core.Internal;

/// <summary>
/// The minimal surface the local HTTP server needs from the webview: the per-launch IPC token, command
/// dispatch, and the JS-eval response channel. Extracted as a seam so the server can be tested in
/// isolation with a fake host (no native webview required).
/// </summary>
internal interface ILocalServerHost
{
    public string IpcToken { get; }

    public Task<(bool Ok, string Data)> DispatchCommandFromServerAsync(string command, string body);

    public void HandleEvalFromServer(long evalId, int ok, string body);
}

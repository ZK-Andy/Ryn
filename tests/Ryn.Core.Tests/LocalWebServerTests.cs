using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Ryn.Core.Internal;
using Xunit;

namespace Ryn.Core.Tests;

/// <summary>
/// End-to-end tests for the hand-rolled loopback HTTP server: drives it with a real HttpClient and a fake
/// host, exercising static serving, SPA fallback, the IPC contract, and token/auth enforcement.
/// </summary>
public sealed class LocalWebServerTests : IAsyncLifetime
{
    private string _contentDir = "";
    private LocalWebServer _server = null!;
    private FakeHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _contentDir = Path.Combine(Path.GetTempPath(), $"ryn-srv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentDir);
        await File.WriteAllTextAsync(Path.Combine(_contentDir, "index.html"), "<html>INDEX</html>");
        await File.WriteAllTextAsync(Path.Combine(_contentDir, "app.js"), "console.log(1)");

        _host = new FakeHost();
        // Port 0 is coerced to the default; pass a high uncommon port and rely on the fallback range.
        _server = new LocalWebServer(_contentDir, preferredPort: 28730);
        _server.SetWebView(_host);
        await _server.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_server.Url) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _server.DisposeAsync();
        try { Directory.Delete(_contentDir, true); } catch (IOException) { }
    }

    [Fact]
    public async Task ServesIndexAtRoot()
    {
        var resp = await _client.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INDEX");
    }

    [Fact]
    public async Task ServesStaticAssetWithMimeType()
    {
        var resp = await _client.GetAsync("/app.js");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/javascript");
    }

    [Fact]
    public async Task UnknownRoute_FallsBackToIndex_ForSpa()
    {
        var resp = await _client.GetAsync("/some/client/route");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("INDEX");
    }

    [Fact]
    public async Task PathTraversal_IsRejected_FallsBackToIndex()
    {
        // A traversal attempt must never escape the content root; it falls back to index.html, never /etc/passwd.
        var resp = await _client.GetAsync("/../../../../etc/passwd");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotContain("root:");
    }

    [Fact]
    public async Task IpcCommand_WithToken_Dispatches_AndReturnsResultInline()
    {
        _host.OnDispatch = (cmd, body) => Task.FromResult((true, $"{{\"cmd\":\"{cmd}\"}}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, "/ipc/cmd/7/fs.readTextFile")
        {
            Content = new StringContent("{\"path\":\"x\"}", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Ryn-Token", _host.IpcToken);

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Be("{\"cmd\":\"fs.readTextFile\"}");
    }

    [Fact]
    public async Task IpcCommand_Error_Returns500_WithMessageInline()
    {
        _host.OnDispatch = (_, _) => Task.FromResult((false, "boom"));

        using var req = new HttpRequestMessage(HttpMethod.Post, "/ipc/cmd/1/x.y")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Ryn-Token", _host.IpcToken);

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await resp.Content.ReadAsStringAsync()).Should().Be("boom");
    }

    [Fact]
    public async Task IpcCommand_WithoutToken_IsForbidden()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/ipc/cmd/1/x.y")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IpcCommand_WithWrongToken_IsForbidden()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/ipc/cmd/1/x.y")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Ryn-Token", "not-the-token");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed class FakeHost : ILocalServerHost
    {
        public string IpcToken { get; } = Guid.NewGuid().ToString("N");
        public Func<string, string, Task<(bool, string)>> OnDispatch { get; set; } =
            (_, _) => Task.FromResult((true, "null"));

        public Task<(bool Ok, string Data)> DispatchCommandFromServerAsync(string command, string body)
            => OnDispatch(command, body);

        public void HandleEvalFromServer(long evalId, int ok, string body) { }
    }
}

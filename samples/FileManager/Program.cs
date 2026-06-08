using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.FileSystem;

var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

// The FileSystem plugin doesn't expand '~', so hand the frontend the real home path.
// Forward slashes keep the JS path logic happy and still resolve on Windows.
#pragma warning disable CA1849 // No async context in top-level statements before builder
var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"))
    .Replace("__RYN_HOME__", homeDir.Replace('\\', '/'), StringComparison.Ordinal);
#pragma warning restore CA1849

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Title = "Ryn File Manager";
        opts.Width = 1000;
        opts.Height = 700;
        opts.Html = html;
        opts.TitleBarStyle = TitleBarStyle.Overlay; // content goes edge-to-edge; traffic lights float over the toolbar
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddRynFileSystem(fs => fs.AllowedPaths.Add(homeDir));
    })
    .Build();

#pragma warning disable CA2007 // Top-level await, no SynchronizationContext
await app.RunAsync();
#pragma warning restore CA2007

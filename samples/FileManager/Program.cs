using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.FileSystem;

#pragma warning disable CA1849 // No async context in top-level statements before builder
var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
#pragma warning restore CA1849

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Title = "Ryn File Manager";
        opts.Width = 1000;
        opts.Height = 700;
        opts.Html = html;
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddRynFileSystem(fs =>
            fs.AllowedPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
    })
    .Build();

#pragma warning disable CA2007 // Top-level await, no SynchronizationContext
await app.RunAsync();
#pragma warning restore CA2007

using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Dialog;
using Ryn.Plugins.FileSystem;

#pragma warning disable CA1849 // No async context in top-level statements before builder
var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
#pragma warning restore CA1849

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Title = "Ryn Markdown Editor";
        opts.Width = 1100;
        opts.Height = 750;
        opts.Html = html;
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddRynFileSystem();
        services.AddRynDialog();
    })
    .Build();

#pragma warning disable CA2007 // Top-level await, no SynchronizationContext
await app.RunAsync();
#pragma warning restore CA2007

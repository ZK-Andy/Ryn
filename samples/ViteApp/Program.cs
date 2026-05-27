using Ryn.Core;
using Ryn.Ipc;
using ViteApp;

// Usage:
//   Development: dotnet run -- --url http://localhost:5173
//   Production:  dotnet run  (loads wwwroot/index.html)

Uri? devUrl = null;
var useDevServer = args.Length >= 2
    && args[0] == "--url"
    && Uri.TryCreate(args[1], UriKind.Absolute, out devUrl);

var builder = RynApplication.CreateBuilder();

if (useDevServer)
{
    builder.ConfigureOptions(opts =>
    {
        opts.Title = "ViteApp (dev)";
        opts.Url = devUrl;
        opts.DevTools = true;
    });
}
else
{
#pragma warning disable CA1849 // No async context in top-level statements before builder
    var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
#pragma warning restore CA1849
    builder.ConfigureOptions(opts =>
    {
        opts.Title = "ViteApp";
        opts.Html = html;
    });
}

var app = builder
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddViteAppCommands();
    })
    .Build();

#pragma warning disable CA2007 // Top-level await, no SynchronizationContext
await app.RunAsync();
#pragma warning restore CA2007

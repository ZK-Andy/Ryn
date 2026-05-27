using Ryn.Core;
using Ryn.Ipc;
using RynApp;

var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Html = html;
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddAppCommands();
    })
    .Build();

await app.RunAsync();

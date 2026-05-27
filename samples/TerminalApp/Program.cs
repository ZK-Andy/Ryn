using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Shell;

#pragma warning disable CA1849
var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
#pragma warning restore CA1849

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Title = "Ryn Terminal";
        opts.Width = 900;
        opts.Height = 600;
        opts.Html = html;
        opts.DevTools = true;
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddRynShell(shell =>
        {
            var defaultShell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
            shell.AllowedCommands.Add(Path.GetFileName(defaultShell));
        });
    })
    .Build();

#pragma warning disable CA2007
await app.RunAsync();
#pragma warning restore CA2007

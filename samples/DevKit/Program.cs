using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Clipboard;
using Ryn.Plugins.Dialog;
using Ryn.Plugins.FileSystem;
using Ryn.Plugins.Notification;
using Ryn.Plugins.Shell;
using DevKit;

#pragma warning disable CA1849
var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
#pragma warning restore CA1849

var app = RynApplication.CreateBuilder()
    .ConfigureOptions(opts =>
    {
        opts.Title = "Ryn DevKit";
        opts.Width = 1280;
        opts.Height = 860;
        opts.Html = html;
        opts.DevTools = true;
    })
    .ConfigureServices(services =>
    {
        services.AddRynCommands();
        services.AddDevKitCommands();
        services.AddRynFileSystem(fs =>
        {
            fs.AllowedPaths.Add(AppContext.BaseDirectory);
            fs.AllowedPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        });
        services.AddRynDialog();
        services.AddRynClipboard();
        services.AddRynShell(shell =>
        {
            shell.AllowedCommands.AddRange(["echo", "date", "whoami", "uname", "ls", "cat", "pwd", "env"]);
            if (OperatingSystem.IsWindows())
                shell.AllowedCommands.AddRange(["cmd.exe", "powershell"]);
            else
                shell.AllowedCommands.AddRange(["bash", "sh"]);
        });
        services.AddRynNotification();
    })
    .Build();

#pragma warning disable CA2007
await app.RunAsync();
#pragma warning restore CA2007

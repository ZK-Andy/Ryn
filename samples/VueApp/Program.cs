using Ryn.Core;
using Ryn.Ipc;
using VueApp;

public static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        var isDev = args.Contains("--dev");

        var app = RynApplication.CreateBuilder()
            .ConfigureOptions(opts =>
            {
                opts.Title = "Ryn + Vue";
                opts.Width = 1100;
                opts.Height = 750;

                if (isDev)
                {
                    opts.Url = new Uri("http://localhost:5173");
                    opts.DevTools = true;
                }
                else
                {
                    opts.ContentDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                }
            })
            .ConfigureServices(services =>
            {
                services.AddRynCommands();
                services.AddAppCommands();
            })
            .Build();

        app.Run();
    }
}

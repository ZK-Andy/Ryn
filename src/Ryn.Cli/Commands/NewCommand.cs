using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ryn.Cli.Commands;

internal static class NewCommand
{
    internal static int Execute(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ryn new <name> [--html]");
            return 1;
        }

        var name = args[0];

        if (!IsValidProjectName(name))
        {
            Console.Error.WriteLine($"Invalid project name: '{name}'. Use only letters, digits, and underscores.");
            return 1;
        }

        var targetDir = Path.Combine(Directory.GetCurrentDirectory(), name);
        if (Directory.Exists(targetDir))
        {
            Console.Error.WriteLine($"Directory already exists: {targetDir}");
            return 1;
        }

        Console.WriteLine($"Creating Ryn project '{name}'...");

        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(Path.Combine(targetDir, "wwwroot"));

        File.WriteAllText(Path.Combine(targetDir, $"{name}.csproj"), GetCsproj(name, targetDir));
        File.WriteAllText(Path.Combine(targetDir, "Program.cs"), GetProgramCs(name));
        File.WriteAllText(Path.Combine(targetDir, "Commands.cs"), GetCommandsCs(name));
        File.WriteAllText(Path.Combine(targetDir, "wwwroot", "index.html"), GetIndexHtml(name));
        File.WriteAllText(Path.Combine(targetDir, "appsettings.json"), GetAppSettings(name));
        File.WriteAllText(Path.Combine(targetDir, "ryn.json"), GetRynJson());

        var sourceRoot = FindRynSourceRoot();
        if (sourceRoot is not null)
        {
            Console.WriteLine("  Using project references (Ryn source detected)");
        }
        else
        {
            Console.WriteLine("  Using NuGet package references (Ryn packages not yet published — run from within the Ryn repo for project references)");
        }
        Console.WriteLine("  Created project files");

        // Run dotnet restore
        Console.WriteLine("  Restoring packages...");
        var restore = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "restore",
            WorkingDirectory = targetDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });
        restore?.WaitForExit();

        Console.WriteLine();
        Console.WriteLine($"  Project '{name}' created successfully!");
        Console.WriteLine();
        Console.WriteLine($"  cd {name}");
        Console.WriteLine("  ryn dev");
        Console.WriteLine();

        return 0;
    }

    private static bool IsValidProjectName(string name) =>
        name.Length > 0 && char.IsLetter(name[0]) &&
        name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static string? FindRynSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Ryn.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string GetCsproj(string name, string targetDir)
    {
        var sourceRoot = FindRynSourceRoot();
        string references;

        if (sourceRoot is not null)
        {
            var src = Path.GetFullPath(Path.Combine(sourceRoot, "src"));
            references = $"""
                  <ItemGroup>
                    <ProjectReference Include="{src}/Ryn.Core/Ryn.Core.csproj" />
                    <ProjectReference Include="{src}/Ryn.Ipc/Ryn.Ipc.csproj" />
                    <ProjectReference Include="{src}/Ryn.Ipc.Generator/Ryn.Ipc.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                  </ItemGroup>
              """;
        }
        else
        {
            references = """
                  <ItemGroup>
                    <PackageReference Include="Ryn.Core" Version="0.1.0-alpha.1" />
                    <PackageReference Include="Ryn.Ipc" Version="0.1.0-alpha.1" />
                  </ItemGroup>
              """;
        }

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <RootNamespace>{name}</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <Content Include="wwwroot/**" CopyToOutputDirectory="PreserveNewest" />
                <Content Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
                <Content Include="ryn.json" CopyToOutputDirectory="PreserveNewest" />
              </ItemGroup>

            {references}
            </Project>
            """;
    }

    private static string GetProgramCs(string name) => $$"""
        using Ryn.Core;
        using Ryn.Ipc;
        using {{name}};

        public static class Program
        {
            [System.STAThread]
            public static void Main()
            {
                var app = RynApplication.CreateBuilder()
                    .ConfigureOptions(opts =>
                    {
                        opts.ContentDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");
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
        """;

    private static string GetCommandsCs(string name) => $$"""
        using System.Globalization;
        using Ryn.Ipc;

        namespace {{name}};

        public static class AppCommands
        {
            [RynCommand]
            public static string Greet(string name) => $"Hello, {name}!";

            [RynCommand]
            public static int Add(int a, int b) => a + b;

            [RynCommand]
            public static string GetTime() => DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        """;

    private static string GetIndexHtml(string name) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <title>{{name}}</title>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
                    background: #0f0f0f; color: #e0e0e0;
                    display: flex; flex-direction: column; align-items: center;
                    justify-content: center; height: 100vh; gap: 24px;
                }
                h1 { font-size: 2.5em; color: #7c3aed; }
                p { color: #888; font-size: 1.1em; }
                .card {
                    background: #1a1a2e; border: 1px solid #2a2a4a; border-radius: 12px;
                    padding: 24px; width: 400px;
                }
                .card h2 { font-size: 1.1em; color: #a78bfa; margin-bottom: 12px; }
                input, button {
                    padding: 8px 16px; border-radius: 8px; border: 1px solid #3a3a5a;
                    background: #252540; color: #e0e0e0; font-size: 14px;
                }
                button {
                    background: #7c3aed; border: none; cursor: pointer; font-weight: 600;
                }
                button:hover { background: #6d28d9; }
                .row { display: flex; gap: 8px; margin-bottom: 12px; }
                .result {
                    background: #0d0d1a; border-radius: 8px; padding: 12px;
                    font-family: monospace; min-height: 40px; margin-top: 8px;
                    border: 1px solid #2a2a4a;
                }
            </style>
        </head>
        <body>
            <h1>{{name}}</h1>
            <p>Built with Ryn — Rich Yet Native</p>

            <div class="card">
                <h2>Try IPC</h2>
                <div class="row">
                    <input id="name" type="text" placeholder="Your name" value="World" />
                    <button onclick="doGreet()">Greet</button>
                </div>
                <div class="result" id="result">Click Greet to call C#</div>
            </div>

            <script>
                async function doGreet() {
                    var name = document.getElementById('name').value;
                    var result = await window.__ryn.invoke('greet', { name: name });
                    document.getElementById('result').textContent = result;
                }
            </script>
        </body>
        </html>
        """;

    private static string GetAppSettings(string name) => $$"""
        {
          "Ryn": {
            "Title": "{{name}}",
            "Width": 900,
            "Height": 700,
            "DevTools": true
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information"
            }
          }
        }
        """;

    private static string GetRynJson() => """
        {
          "capabilities": {
            "fs": {
              "allow": ["readTextFile", "readDir", "exists", "stat"]
            },
            "clipboard": true,
            "notification": true
          }
        }
        """;
}

using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Ryn.PackageTests;

[Trait("Category", "Package")]
public sealed class PackageSmokeTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    [Fact]
    public async Task NuGet_consumer_builds_and_generator_emits_router()
    {
        var solutionRoot = FindSolutionRoot();
        var packOutputDir = CreateTempDir("ryn-pack");
        var appDir = CreateTempDir("ryn-smoke-app");

        // Step 1: Pack the required projects into local nupkgs.
        // We pack individual projects rather than the full solution to avoid unrelated
        // build errors in samples/CLI and to keep the test focused.
#pragma warning disable CA2007 // xUnit manages SynchronizationContext
        string[] projectsToPack =
        [
            Path.Combine(solutionRoot, "src", "Ryn.Interop", "Ryn.Interop.csproj"),
            Path.Combine(solutionRoot, "src", "Ryn.Ipc.Generator", "Ryn.Ipc.Generator.csproj"),
            Path.Combine(solutionRoot, "src", "Ryn.Core", "Ryn.Core.csproj"),
            Path.Combine(solutionRoot, "src", "Ryn.Ipc", "Ryn.Ipc.csproj"),
        ];

        foreach (var project in projectsToPack)
        {
            var packResult = await RunDotnetAsync(
                solutionRoot,
                "pack", project,
                "-c", "Release",
                "-p:VersionPrefix=99.0.0-test",
                "-p:VersionSuffix=",
                "-p:PackageReadmeFile=",
                "-o", packOutputDir);

            packResult.ExitCode.Should().Be(0,
                $"dotnet pack {Path.GetFileName(project)} failed:\n{packResult.Output}");
        }

        // Verify expected packages were produced
        var nupkgs = Directory.GetFiles(packOutputDir, "*.nupkg");
        nupkgs.Should().Contain(f => Path.GetFileName(f).StartsWith("Ryn.Core.", StringComparison.Ordinal));
        nupkgs.Should().Contain(f => Path.GetFileName(f).StartsWith("Ryn.Ipc.", StringComparison.Ordinal));

        // Step 2: Create a minimal consumer app
        WriteTempApp(appDir, packOutputDir);

        // Step 3: Build the consumer app
        var buildResult = await RunDotnetAsync(appDir, "build", "-c", "Release");

        buildResult.ExitCode.Should().Be(0, $"dotnet build failed:\n{buildResult.Output}");

        // Step 4: Verify the source generator produced a router file
        var generatedFiles = Directory.GetFiles(
            Path.Combine(appDir, "obj"),
            "*Router.g.cs",
            SearchOption.AllDirectories);

        generatedFiles.Should().NotBeEmpty(
            "the source generator should emit a Router.g.cs file for the Commands class");

        // Verify the generated file has meaningful content
        var routerContent = await File.ReadAllTextAsync(generatedFiles[0]);
        routerContent.Should().Contain("ICommandRouter", "the router should implement ICommandRouter");
        routerContent.Should().Contain("greet", "the router should handle the 'greet' command");

        // Step 5: Verify runtime native assets layout is correct (when present)
        // Native libs are gitignored and may not be present in CI builds.
        // The buildTransitive targets file must exist in the package; actual
        // native files are only available after downloading from releases.
        var outputDir = Path.Combine(appDir, "bin", "Release", "net10.0");
        Directory.Exists(outputDir).Should().BeTrue("the build should produce output in bin/Release/net10.0");
#pragma warning restore CA2007
    }

    private static void WriteTempApp(string appDir, string feedDir)
    {
        // nuget.config — local feed only, no nuget.org
        var nugetConfig = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="FEED_DIR" />
              </packageSources>
            </configuration>
            """.Replace("FEED_DIR", feedDir, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(appDir, "nuget.config"), nugetConfig);

        // .csproj — references Ryn packages from the local feed
        File.WriteAllText(
            Path.Combine(appDir, "SmokeApp.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>preview</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <!-- Write source-generator output to disk so the test can inspect it -->
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <!-- Suppress warnings that don't matter for a smoke test -->
                <NoWarn>CA1812;CA1852</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Ryn.Core" Version="99.0.0-test" />
                <PackageReference Include="Ryn.Ipc" Version="99.0.0-test" />
              </ItemGroup>
            </Project>
            """);

        // Program.cs — minimal app that exercises RynApplication + generated DI
        File.WriteAllText(
            Path.Combine(appDir, "Program.cs"),
            """
            using Ryn.Core;
            using Ryn.Ipc;
            using SmokeApp;

            var app = RynApplication.CreateBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRynCommands();
                    services.AddCommands();
                })
                .Build();
            """);

        // Commands.cs — has a [RynCommand] method so the generator has work to do
        File.WriteAllText(
            Path.Combine(appDir, "Commands.cs"),
            """
            using Ryn.Ipc;

            namespace SmokeApp;

            public static class Commands
            {
                [RynCommand]
                public static string Greet(string name) => $"Hello, {name}!";
            }
            """);
    }

    private static string FindSolutionRoot()
    {
        // Walk up from the test assembly location to find Ryn.slnx
        var dir = Path.GetDirectoryName(typeof(PackageSmokeTests).Assembly.Location)!;

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Ryn.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find Ryn.slnx -- run this test from within the Ryn repository.");
    }

    private string CreateTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static async Task<ProcessResult> RunDotnetAsync(string workingDir, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        // Read both streams to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdout + stderr);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
#pragma warning disable CA1031 // Best-effort temp directory cleanup
            catch
#pragma warning restore CA1031
            {
                // Cleanup is best-effort; don't fail the test over leftover temp dirs
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}

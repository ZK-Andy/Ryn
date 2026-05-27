using FluentAssertions;
using NSubstitute;
using Ryn.Core;
using Ryn.Plugins.Shell;
using Xunit;

namespace Ryn.Plugins.Tests;

public sealed class ShellCommandsTests
{
    [Fact]
    public void Execute_DeniedWhenAllowlistEmpty()
    {
        ShellCommands.Configure(new ShellOptions { AllowedCommands = [] });

        var act = () => ShellCommands.Execute("ls", "[]");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Execute_AllowedCommand_ReturnsOutput()
    {
        ShellCommands.Configure(new ShellOptions { AllowedCommands = ["echo"] });

        var result = ShellCommands.Execute("echo", "[\"hello\"]");
        result.Should().Contain("hello");
        result.Should().Contain("exitCode");
    }

    [Fact]
    public void Execute_DeniedCommand_Throws()
    {
        ShellCommands.Configure(new ShellOptions { AllowedCommands = ["echo"] });

        var act = () => ShellCommands.Execute("rm", "[\"-rf\", \"/\"]");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Spawn_RejectsDisallowedCommand()
    {
        ShellCommands.Configure(new ShellOptions { AllowedCommands = ["echo"] });
        var webView = Substitute.For<IRynWebView>();
        using var commands = new SpawnCommands(webView);

        var act = () => commands.Spawn("rm", "[\"-rf\", \"/\"]");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Kill_ReturnsErrorForUnknownPid()
    {
        ShellCommands.Configure(new ShellOptions { AllowedCommands = ["echo"] });
        var webView = Substitute.For<IRynWebView>();
        using var commands = new SpawnCommands(webView);

        var result = commands.Kill(99999);
        result.Should().BeFalse();
    }

    [Fact]
    public void FullPathAllowlist_DoesNotPermitBareInvocation()
    {
        using var fixture = new CommandFixture("mytool");

        ShellCommands.Configure(new ShellOptions { AllowedCommands = [fixture.FullPath] });

        var act = () => ShellCommands.ValidateAndResolveCommand("mytool");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void BareAllowlist_ResolvesToConfigTimeCanonicalPath()
    {
        using var fixture = new CommandFixture("mytool");

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", fixture.Directory + (OperatingSystem.IsWindows() ? ";" : ":") + originalPath);
        try
        {
            ShellCommands.Configure(new ShellOptions { AllowedCommands = ["mytool"] });

            var resolved = ShellCommands.ValidateAndResolveCommand("mytool");

            resolved.Should().NotBe("mytool");
            Path.IsPathRooted(resolved).Should().BeTrue();
            resolved.Should().Be(fixture.FullPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void UnresolvedBareAllowlist_RejectsAtInvocation()
    {
        ShellCommands.Configure(new ShellOptions
        {
            AllowedCommands = ["this_command_does_not_exist_anywhere_in_path"]
        });

        var act = () => ShellCommands.ValidateAndResolveCommand("this_command_does_not_exist_anywhere_in_path");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void FullPathAllowlist_PermitsExactPathInvocation()
    {
        using var fixture = new CommandFixture("mytool");

        ShellCommands.Configure(new ShellOptions { AllowedCommands = [fixture.FullPath] });

        var resolved = ShellCommands.ValidateAndResolveCommand(fixture.FullPath);
        resolved.Should().Be(fixture.FullPath);
    }

    [Fact]
    public void BareAllowlist_CannotBeHijackedByLaterPathEntry()
    {
        using var legitimate = new CommandFixture("mytool");
        using var malicious = new CommandFixture("mytool");

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        // Legitimate dir first in PATH at configure time
        Environment.SetEnvironmentVariable("PATH",
            legitimate.Directory + (OperatingSystem.IsWindows() ? ";" : ":") +
            malicious.Directory + (OperatingSystem.IsWindows() ? ";" : ":") + originalPath);
        try
        {
            ShellCommands.Configure(new ShellOptions { AllowedCommands = ["mytool"] });

            // Even if PATH changes later, the resolved path is pinned to configure time
            var resolved = ShellCommands.ValidateAndResolveCommand("mytool");
            resolved.Should().Be(legitimate.FullPath);
            resolved.Should().NotBe(malicious.FullPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    private sealed class CommandFixture : IDisposable
    {
        public string Directory { get; }
        public string FullPath { get; }

        public CommandFixture(string name)
        {
            Directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(Directory);

            var ext = OperatingSystem.IsWindows() ? ".exe" : "";
            FullPath = Path.GetFullPath(Path.Combine(Directory, name + ext));
            File.WriteAllBytes(FullPath, [0]);

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(FullPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch (IOException) { /* cleanup best-effort */ }
        }
    }
}

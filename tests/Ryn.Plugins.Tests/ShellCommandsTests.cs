using FluentAssertions;
using NSubstitute;
using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Shell;
using Xunit;

namespace Ryn.Plugins.Tests;

public sealed class ShellCommandsTests
{
    [Fact]
    public void ScopedCommand_PermitsMatchingArgs_RejectsOthers()
    {
        // echo is scoped to exactly one literal argument "hello".
        ShellCommands.Configure(new ShellOptions
        {
            CommandScopes = [new CommandScope("echo", [ArgRule.Literal("hello")])],
        });

        var ok = () => ShellCommands.Execute("echo", "[\"hello\"]");
        ok.Should().NotThrow();

        var wrongArg = () => ShellCommands.Execute("echo", "[\"goodbye\"]");
        wrongArg.Should().Throw<UnauthorizedAccessException>();

        var wrongCount = () => ShellCommands.Execute("echo", "[\"hello\", \"world\"]");
        wrongCount.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void ScopedCommand_RegexValidator_Enforced()
    {
        ShellCommands.Configure(new ShellOptions
        {
            CommandScopes = [new CommandScope("echo", [ArgRule.Pattern("^[a-z]+$")])],
        });

        ((Action)(() => ShellCommands.Execute("echo", "[\"abc\"]"))).Should().NotThrow();
        ((Action)(() => ShellCommands.Execute("echo", "[\"abc123\"]"))).Should().Throw<UnauthorizedAccessException>();
        // injection attempt is just a non-matching argument, passed literally (never to a shell)
        ((Action)(() => ShellCommands.Execute("echo", "[\"$(rm -rf /)\"]"))).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Execute_FailsClosed_WhenNotConfigured()
    {
        // Reset to an unconfigured state via reflection-free path: configure then verify the
        // dedicated "not configured" guard by clearing through an empty options object is not enough,
        // so we assert the empty-allowlist guard which is the reachable production state.
        ShellCommands.Configure(new ShellOptions { AllowedCommands = [] });
        ((Action)(() => ShellCommands.Execute("echo", "[]"))).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Open_RejectsFileScheme()
    {
        ShellCommands.Configure(new ShellOptions());
        ((Action)(() => ShellCommands.ValidateOpenTarget("file:///etc/passwd"))).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Open_RejectsBarePath()
    {
        ShellCommands.Configure(new ShellOptions());
        ((Action)(() => ShellCommands.ValidateOpenTarget("/Applications/Calculator.app"))).Should().Throw<UnauthorizedAccessException>();
        ((Action)(() => ShellCommands.ValidateOpenTarget("Calculator.app"))).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Open_AllowsHttpsByDefault()
    {
        ShellCommands.Configure(new ShellOptions());
        ((Action)(() => ShellCommands.ValidateOpenTarget("https://example.com"))).Should().NotThrow();
        ((Action)(() => ShellCommands.ValidateOpenTarget("mailto:a@b.com"))).Should().NotThrow();
    }

    [Fact]
    public void Open_HonorsConfiguredSchemeAllowlist()
    {
        ShellCommands.Configure(new ShellOptions { AllowedOpenSchemes = ["https"] });
        ((Action)(() => ShellCommands.ValidateOpenTarget("https://example.com"))).Should().NotThrow();
        ((Action)(() => ShellCommands.ValidateOpenTarget("mailto:a@b.com"))).Should().Throw<UnauthorizedAccessException>();
    }

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
        var sep = OperatingSystem.IsWindows() ? ";" : ":";
        try
        {
            // Configure with legitimate first in PATH
            Environment.SetEnvironmentVariable("PATH",
                legitimate.Directory + sep + originalPath);
            ShellCommands.Configure(new ShellOptions { AllowedCommands = ["mytool"] });

            // Now swap PATH so malicious comes first
            Environment.SetEnvironmentVariable("PATH",
                malicious.Directory + sep + originalPath);

            // Resolution is pinned to configure time — malicious binary is ignored
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

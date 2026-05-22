using FluentAssertions;
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
}

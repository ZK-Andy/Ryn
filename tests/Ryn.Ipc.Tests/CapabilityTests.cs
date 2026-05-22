using FluentAssertions;
using Xunit;

namespace Ryn.Ipc.Tests;

public sealed class CapabilityTests
{
    [Fact]
    public void AllowAll_PermitsEverything()
    {
        var caps = RynCapabilities.AllowAll();

        var act = () => caps.ThrowIfDenied("fs.readTextFile");
        act.Should().NotThrow();
    }

    [Fact]
    public void MissingPlugin_Denied()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>());

        var act = () => caps.ThrowIfDenied("fs.readTextFile");
        act.Should().Throw<RynCommandDeniedException>()
            .Where(e => e.Command == "fs.readTextFile");
    }

    [Fact]
    public void AllowTrue_PermitsAllCommands()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>
        {
            ["fs"] = new() { AllowAll = true },
        });

        var act = () => caps.ThrowIfDenied("fs.readTextFile");
        act.Should().NotThrow();
    }

    [Fact]
    public void AllowList_PermitsOnlyListed()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>
        {
            ["fs"] = new() { Allow = new HashSet<string> { "readTextFile", "exists" } },
        });

        var allowed = () => caps.ThrowIfDenied("fs.readTextFile");
        allowed.Should().NotThrow();

        var denied = () => caps.ThrowIfDenied("fs.remove");
        denied.Should().Throw<RynCommandDeniedException>();
    }

    [Fact]
    public void DenyList_BlocksListed()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>
        {
            ["fs"] = new() { AllowAll = true, Deny = new HashSet<string> { "remove" } },
        });

        var allowed = () => caps.ThrowIfDenied("fs.readTextFile");
        allowed.Should().NotThrow();

        var denied = () => caps.ThrowIfDenied("fs.remove");
        denied.Should().Throw<RynCommandDeniedException>();
    }

    [Fact]
    public void AllowAndDeny_DenyTakesPrecedence()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>
        {
            ["fs"] = new()
            {
                Allow = new HashSet<string> { "readTextFile", "remove" },
                Deny = new HashSet<string> { "remove" },
            },
        });

        var allowed = () => caps.ThrowIfDenied("fs.readTextFile");
        allowed.Should().NotThrow();

        var denied = () => caps.ThrowIfDenied("fs.remove");
        denied.Should().Throw<RynCommandDeniedException>();
    }

    [Fact]
    public void NoPrefix_Denied()
    {
        var caps = RynCapabilities.FromRules(new Dictionary<string, CapabilityRule>());

        var act = () => caps.ThrowIfDenied("commandWithoutDot");
        act.Should().Throw<RynCommandDeniedException>()
            .WithMessage("*no plugin prefix*");
    }

    [Fact]
    public void LoadFromJson_ParsesCorrectly()
    {
        var json = """
        {
          "capabilities": {
            "fs": {
              "allow": ["readTextFile", "exists"],
              "deny": ["remove"]
            },
            "dialog": true,
            "shell": false
          }
        }
        """;

        var caps = RynCapabilitiesLoader.Parse(json);

        // fs.readTextFile — allowed
        var act1 = () => caps.ThrowIfDenied("fs.readTextFile");
        act1.Should().NotThrow();

        // fs.remove — denied
        var act2 = () => caps.ThrowIfDenied("fs.remove");
        act2.Should().Throw<RynCommandDeniedException>();

        // fs.writeTextFile — not in allow list
        var act3 = () => caps.ThrowIfDenied("fs.writeTextFile");
        act3.Should().Throw<RynCommandDeniedException>();

        // dialog.message — allowed (true = all)
        var act4 = () => caps.ThrowIfDenied("dialog.message");
        act4.Should().NotThrow();

        // shell.execute — denied (false = none)
        var act5 = () => caps.ThrowIfDenied("shell.execute");
        act5.Should().Throw<RynCommandDeniedException>();
    }

    [Fact]
    public void MissingFile_ReturnsAllowAll()
    {
        // RynCapabilitiesLoader.Load() with no ryn.json returns AllowAll
        // We test the Parse path with no capabilities section
        var caps = RynCapabilitiesLoader.Parse("{}");

        var act = () => caps.ThrowIfDenied("anything.goes");
        act.Should().NotThrow();
    }

    [Fact]
    public void DenyOnlyRule_AllowsEverythingExceptDenied()
    {
        var json = """
        {
          "capabilities": {
            "fs": {
              "deny": ["remove", "writeTextFile"]
            }
          }
        }
        """;

        var caps = RynCapabilitiesLoader.Parse(json);

        var allowed = () => caps.ThrowIfDenied("fs.readTextFile");
        allowed.Should().NotThrow();

        var denied = () => caps.ThrowIfDenied("fs.remove");
        denied.Should().Throw<RynCommandDeniedException>();
    }
}

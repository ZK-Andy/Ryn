using FluentAssertions;
using Ryn.Plugins.FileSystem;
using Xunit;

namespace Ryn.Plugins.Tests;

public sealed class FileSystemCommandsTests : IDisposable
{
    private readonly string _testDir;

    public FileSystemCommandsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ryn-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        PathValidator.Configure(new FileSystemOptions { AllowedPaths = [_testDir] });
    }

    [Fact]
    public void WriteAndReadTextFile_RoundTrips()
    {
        var path = Path.Combine(_testDir, "test.txt");
        FileSystemCommands.WriteTextFile(path, "hello ryn");

        var content = FileSystemCommands.ReadTextFile(path);
        content.Should().Be("hello ryn");
    }

    [Fact]
    public void Exists_ReturnsTrueForExistingFile()
    {
        var path = Path.Combine(_testDir, "exists.txt");
        File.WriteAllText(path, "x");

        FileSystemCommands.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Exists_ReturnsFalseForMissing()
    {
        FileSystemCommands.Exists(Path.Combine(_testDir, "nope.txt")).Should().BeFalse();
    }

    [Fact]
    public void MkDir_CreatesDirectory()
    {
        var path = Path.Combine(_testDir, "subdir");
        FileSystemCommands.MkDir(path);

        Directory.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Remove_DeletesFile()
    {
        var path = Path.Combine(_testDir, "todelete.txt");
        File.WriteAllText(path, "x");

        FileSystemCommands.Remove(path);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void ReadDir_ReturnsJsonEntries()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "a");
        Directory.CreateDirectory(Path.Combine(_testDir, "sub"));

        var json = FileSystemCommands.ReadDir(_testDir);
        json.Should().Contain("a.txt");
        json.Should().Contain("sub");
    }

    [Fact]
    public void Stat_ReturnsJsonInfo()
    {
        var path = Path.Combine(_testDir, "stat.txt");
        File.WriteAllText(path, "content");

        var json = FileSystemCommands.Stat(path);
        json.Should().Contain("stat.txt");
        json.Should().Contain("\"isDirectory\":false");
    }

    [Fact]
    public void PathTraversal_Rejected()
    {
        var act = () => FileSystemCommands.ReadTextFile(Path.Combine(_testDir, "..", "..", "etc", "passwd"));
        act.Should().Throw<UnauthorizedAccessException>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch (IOException) { /* cleanup */ }
    }
}

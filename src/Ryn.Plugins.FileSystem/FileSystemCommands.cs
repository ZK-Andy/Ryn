using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ryn.Ipc;

namespace Ryn.Plugins.FileSystem;

public sealed class FileSystemCommands
{
    private readonly PathValidator _validator;

    public FileSystemCommands(PathValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    [RynCommand("fs.readTextFile")]
    public string ReadTextFile(string path)
    {
        var resolved = _validator.ResolveForRead(path);
        return File.ReadAllText(resolved);
    }

    [RynCommand("fs.readFile")]
    public string ReadFile(string path)
    {
        var resolved = _validator.ResolveForRead(path);
        return Convert.ToBase64String(File.ReadAllBytes(resolved));
    }

    [RynCommand("fs.writeFile")]
    public string WriteFile(string path, string data)
    {
        var resolved = _validator.Resolve(path);
        var dir = Path.GetDirectoryName(resolved);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllBytes(resolved, Convert.FromBase64String(data));
        return resolved;
    }

    [RynCommand("fs.writeTextFile")]
    public void WriteTextFile(string path, string text)
    {
        var resolved = _validator.Resolve(path);
        var dir = Path.GetDirectoryName(resolved);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(resolved, text);
    }

    [RynCommand("fs.exists")]
    public bool Exists(string path)
    {
        var resolved = _validator.Resolve(path);
        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    [RynCommand("fs.mkdir")]
    public void MkDir(string path)
    {
        var resolved = _validator.Resolve(path);
        Directory.CreateDirectory(resolved);
    }

    [RynCommand("fs.remove")]
    public void Remove(string path)
    {
        var resolved = _validator.Resolve(path);
        if (File.Exists(resolved))
            File.Delete(resolved);
        else if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }

    [RynCommand("fs.readDir")]
    public string ReadDir(string path)
    {
        var resolved = _validator.Resolve(path);
        var entries = new List<FileEntry>();

        foreach (var entry in new DirectoryInfo(resolved).EnumerateFileSystemInfos())
        {
            entries.Add(new FileEntry(
                entry.Name,
                entry.FullName,
                entry is DirectoryInfo,
                entry is FileInfo fi ? fi.Length : 0,
                entry.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)));
        }

        return JsonSerializer.Serialize(entries, FsJsonContext.Default.ListFileEntry);
    }

    [RynCommand("fs.stat")]
    public string Stat(string path)
    {
        var resolved = _validator.Resolve(path);
        var info = new FileInfo(resolved);
        var isDir = Directory.Exists(resolved);

        var stat = new FileStat(
            info.Name,
            info.FullName,
            isDir,
            isDir ? 0 : info.Length,
            info.CreationTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            info.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            info.LastAccessTimeUtc.ToString("O", CultureInfo.InvariantCulture));

        return JsonSerializer.Serialize(stat, FsJsonContext.Default.FileStat);
    }
}

internal record FileEntry(string Name, string Path, bool IsDirectory, long Size, string Modified);
internal record FileStat(string Name, string Path, bool IsDirectory, long Size, string Created, string Modified, string Accessed);

[JsonSerializable(typeof(List<FileEntry>))]
[JsonSerializable(typeof(FileStat))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class FsJsonContext : JsonSerializerContext;

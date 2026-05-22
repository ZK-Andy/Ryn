using System.Globalization;
using System.Text.Json;
using Ryn.Ipc;

namespace Ryn.Plugins.FileSystem;

public static class FileSystemCommands
{
    [RynCommand("fs.readTextFile")]
    public static string ReadTextFile(string path)
    {
        var resolved = PathValidator.Resolve(path);
        return File.ReadAllText(resolved);
    }

    [RynCommand("fs.writeTextFile")]
    public static void WriteTextFile(string path, string text)
    {
        var resolved = PathValidator.Resolve(path);
        var dir = Path.GetDirectoryName(resolved);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(resolved, text);
    }

    [RynCommand("fs.exists")]
    public static bool Exists(string path)
    {
        var resolved = PathValidator.Resolve(path);
        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    [RynCommand("fs.mkdir")]
    public static void MkDir(string path)
    {
        var resolved = PathValidator.Resolve(path);
        Directory.CreateDirectory(resolved);
    }

    [RynCommand("fs.remove")]
    public static void Remove(string path)
    {
        var resolved = PathValidator.Resolve(path);
        if (File.Exists(resolved))
            File.Delete(resolved);
        else if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }

    [RynCommand("fs.readDir")]
    public static string ReadDir(string path)
    {
        var resolved = PathValidator.Resolve(path);
        var entries = new List<object>();

        foreach (var entry in new DirectoryInfo(resolved).EnumerateFileSystemInfos())
        {
            entries.Add(new
            {
                name = entry.Name,
                path = entry.FullName,
                isDirectory = entry is DirectoryInfo,
                size = entry is FileInfo fi ? fi.Length : 0,
                modified = entry.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        return JsonSerializer.Serialize(entries);
    }

    [RynCommand("fs.stat")]
    public static string Stat(string path)
    {
        var resolved = PathValidator.Resolve(path);
        var info = new FileInfo(resolved);
        var isDir = Directory.Exists(resolved);

        var stat = new
        {
            name = info.Name,
            path = info.FullName,
            isDirectory = isDir,
            size = isDir ? 0 : info.Length,
            created = info.CreationTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            modified = info.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture),
            accessed = info.LastAccessTimeUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        return JsonSerializer.Serialize(stat);
    }
}

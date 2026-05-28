using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ryn.Core.Internal;

internal sealed class WindowStatePersistence
{
    private readonly string _filePath;

    internal WindowStatePersistence(string appId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ryn", appId);
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "window-state.json");
    }

    internal WindowStateData? Load()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, WindowStateJsonContext.Default.WindowStateData);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    internal void Save(WindowStateData state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, WindowStateJsonContext.Default.WindowStateData);
            File.WriteAllText(_filePath, json);
        }
        catch (IOException) { }
    }
}

internal sealed class WindowStateData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMaximized { get; set; }
}

[JsonSerializable(typeof(WindowStateData))]
internal sealed partial class WindowStateJsonContext : JsonSerializerContext;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;

namespace Ryn.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonSerializationBenchmarks
{
    private byte[] _simpleArgs = null!;
    private byte[] _multiArgs = null!;
    private string _escapeInput = null!;
    private string _cleanInput = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleArgs = Encoding.UTF8.GetBytes("""{"value":42}""");
        _multiArgs = Encoding.UTF8.GetBytes("""{"name":"benchmark","count":100,"enabled":true}""");
        _escapeInput = "hello \"world\" with \\backslash";
        _cleanInput = "hello world";
    }

    [Benchmark(Description = "Parse + GetInt32 (single param)")]
    public int ParseSingleParam()
    {
        using var doc = JsonDocument.Parse(_simpleArgs);
        return doc.RootElement.GetProperty("value").GetInt32();
    }

    [Benchmark(Description = "Parse + extract 3 params (string, int, bool)")]
    public (string, int, bool) ParseMultipleParams()
    {
        using var doc = JsonDocument.Parse(_multiArgs);
        var root = doc.RootElement;
        var name = root.GetProperty("name").GetString()!;
        var count = root.GetProperty("count").GetInt32();
        var enabled = root.GetProperty("enabled").GetBoolean();
        return (name, count, enabled);
    }

    [Benchmark(Description = "Parse from ReadOnlyMemory<byte>")]
    public int ParseFromMemory()
    {
        ReadOnlyMemory<byte> memory = _simpleArgs;
        using var doc = JsonDocument.Parse(memory);
        return doc.RootElement.GetProperty("value").GetInt32();
    }

    [Benchmark(Description = "__ToJson string escaping")]
    public string ToJsonEscape()
    {
        return ToJson(_escapeInput);
    }

    [Benchmark(Description = "__ToJson clean string (no escaping)")]
    public string ToJsonClean()
    {
        return ToJson(_cleanInput);
    }

    [Benchmark(Description = "JsonSerializer.Serialize (source-gen, reference)")]
    public string SerializerReference()
    {
        return JsonSerializer.Serialize(_escapeInput, BenchmarkJsonContext.Default.String);
    }

    // Mirrors the __ToJson pattern emitted by the source generator
    private static string ToJson(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

[JsonSerializable(typeof(string))]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext;


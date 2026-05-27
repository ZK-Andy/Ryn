using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ryn.Ipc;

namespace DevKit;

[RynJsonContext(typeof(DevKitJsonContext))]
internal static class DevKitCommands
{
    [RynCommand("devkit.systemInfo")]
    public static SystemInfo GetSystemInfo() => new(
        Environment.MachineName,
        RuntimeInformation.OSDescription,
        RuntimeInformation.RuntimeIdentifier,
        RuntimeInformation.FrameworkDescription,
        Environment.ProcessorCount,
        Environment.WorkingSet / 1024 / 1024);

    [RynCommand("devkit.environment")]
    public static string[] GetEnvironmentVars()
    {
        var vars = Environment.GetEnvironmentVariables();
        var result = new string[vars.Count];
        var i = 0;
        foreach (System.Collections.DictionaryEntry entry in vars)
        {
            result[i++] = $"{entry.Key}={entry.Value}";
        }
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    [RynCommand("devkit.echo")]
    public static string Echo(string message) => message;

    [RynCommand("devkit.add")]
    public static int Add(int a, int b) => a + b;

    [RynCommand("devkit.multiply")]
    public static double Multiply(double a, double b) => a * b;

    [RynCommand("devkit.reverse")]
    public static string[] ReverseArray(string[] items)
    {
        Array.Reverse(items);
        return items;
    }

    [RynCommand("devkit.timestamp")]
    public static string Timestamp() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    [RynCommand("devkit.nullableTest")]
    public static int? NullableTest(int value) => value == 0 ? null : value * 2;

    [RynCommand("devkit.jsonTest")]
    public static JsonElement JsonTest(JsonElement data) => data;

    [RynCommand("devkit.createDto")]
    public static SystemInfo CreateDto(string name) => new(
        name,
        "custom",
        "test-rid",
        ".NET test",
        1,
        0);

    [RynCommand("devkit.benchmark")]
    public static BenchmarkResult RunBenchmark(int iterations)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sum = 0L;
        for (var i = 0; i < iterations; i++)
            sum += i;
        sw.Stop();
        return new BenchmarkResult(
            iterations,
            sw.Elapsed.TotalMilliseconds,
            sum);
    }
}

internal sealed record SystemInfo(
    string MachineName,
    string Os,
    string Rid,
    string Framework,
    int ProcessorCount,
    long MemoryMb);

internal sealed record BenchmarkResult(
    int Iterations,
    double ElapsedMs,
    long Result);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SystemInfo))]
[JsonSerializable(typeof(BenchmarkResult))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class DevKitJsonContext : JsonSerializerContext;

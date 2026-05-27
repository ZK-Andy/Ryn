using BenchmarkDotNet.Attributes;
using Ryn.Core.Internal;

namespace Ryn.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public unsafe class Utf8StringBenchmarks
{
    private string _short = null!;
    private string _medium = null!;
    private string _long = null!;
    private string _unicode = null!;

    [GlobalSetup]
    public void Setup()
    {
        _short = "ryn://app";
        _medium = "https://example.com/some/path/to/resource?query=value&other=123";
        _long = new string('x', 1024);
        _unicode = "Hello éèê 世界 🌍 end";
    }

    [Benchmark(Description = "Short string (9 chars, stack path)")]
    public void CreateShort()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create(_short, buf);
        _ = str.Pointer;
        str.Dispose();
    }

    [Benchmark(Description = "Medium string (63 chars, stack path)")]
    public void CreateMedium()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create(_medium, buf);
        _ = str.Pointer;
        str.Dispose();
    }

    [Benchmark(Description = "Long string (1024 chars, pooled path)")]
    public void CreateLong()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create(_long, buf);
        _ = str.Pointer;
        str.Dispose();
    }

    [Benchmark(Description = "Unicode string (multi-byte, stack path)")]
    public void CreateUnicode()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create(_unicode, buf);
        _ = str.Pointer;
        str.Dispose();
    }

    [Benchmark(Description = "Short string, tight buffer")]
    public void CreateShortTightBuffer()
    {
        Span<byte> buf = stackalloc byte[16];
        var str = Utf8String.Create(_short, buf);
        _ = str.Pointer;
        str.Dispose();
    }

    [Benchmark(Description = "Long string, tiny buffer (forces pool)")]
    public void CreateLongTinyBuffer()
    {
        Span<byte> buf = stackalloc byte[16];
        var str = Utf8String.Create(_long, buf);
        _ = str.Pointer;
        str.Dispose();
    }
}
